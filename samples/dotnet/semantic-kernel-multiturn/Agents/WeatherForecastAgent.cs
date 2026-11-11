// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelMultiturn.Plugins;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SemanticKernelMultiturn.Agents;

public class WeatherForecastAgent
{
    private const int MaximumHistoryMessages = 20;
    private const int MaximumFormatAttempts = 2;
    private const string AgentName = "WeatherForecastAgent";
    private const string AgentInstructions = """
        You are a friendly assistant that helps people find a weather forecast for a given time and place.
        Ask follow-up questions until you have both a location and a date. Once you have enough information,
        use the weather forecast tool and adaptive card tool, then return the result as an Adaptive Card.

        The Adaptive Card must use version 1.5 and include the location, date, temperature in Celsius and
        Fahrenheit, and a button for more details. The button must point to
        https://www.msn.com/en-us/weather/forecast/in-{location}, replacing {location} with a URL-encoded location.

        Respond only in JSON using one of these shapes:
        { "contentType": "Text", "content": "Follow-up question" }
        { "contentType": "AdaptiveCard", "content": { "type": "AdaptiveCard", "version": "1.5" } }
        """;

    private readonly ChatCompletionAgent _agent;

    public WeatherForecastAgent(Kernel kernel, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(services);

        Kernel turnKernel = kernel.Clone();
        turnKernel.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>(serviceProvider: services));
        turnKernel.Plugins.Add(KernelPluginFactory.CreateFromType<WeatherForecastPlugin>(serviceProvider: services));
        turnKernel.Plugins.Add(KernelPluginFactory.CreateFromType<AdaptiveCardPlugin>(serviceProvider: services));

        this._agent = new ChatCompletionAgent
        {
            Instructions = AgentInstructions,
            Name = AgentName,
            Kernel = turnKernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = "json_object",
                Temperature = 0,
                TopP = 1
            })
        };
    }

    /// <summary>
    /// Invokes the agent with the given input and returns the response.
    /// </summary>
    /// <param name="input">A message to process.</param>
    /// <returns>An instance of <see cref="WeatherForecastAgentResponse"/></returns>
    public async Task<WeatherForecastAgentResponse> InvokeAgentAsync(string input, ChatHistory chatHistory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(chatHistory);

        chatHistory.Add(new ChatMessageContent(AuthorRole.User, input));
        TrimHistory(chatHistory);
        AgentThread thread = new ChatHistoryAgentThread();

        for (int attempt = 0; attempt < MaximumFormatAttempts; attempt++)
        {
            TrimHistory(chatHistory);
            string responseText = string.Empty;
            await foreach (ChatMessageContent response in this._agent.InvokeAsync(chatHistory, thread: thread))
            {
                chatHistory.Add(response);
                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    responseText = response.Content;
                }
            }

            if (TryParseResponse(responseText, out WeatherForecastAgentResponse? result, out string validationFailure))
            {
                TrimHistory(chatHistory);
                return result!;
            }

            if (attempt + 1 < MaximumFormatAttempts)
            {
                chatHistory.Add(new ChatMessageContent(
                    AuthorRole.User,
                    "The previous response did not match the required JSON schema. Return only a valid response object."));
            }
        }

        TrimHistory(chatHistory);
        throw new InvalidOperationException("The model did not return a valid weather response.");
    }

    private static bool TryParseResponse(
        string value,
        out WeatherForecastAgentResponse? response,
        out string validationFailure)
    {
        response = null;
        validationFailure = string.Empty;
        try
        {
            WeatherForecastAgentResponse? textResponse = null;
            WeatherForecastAgentResponse? adaptiveCardResponse = null;
            string? latestFailure = null;
            int responseCount = 0;
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(RemoveMarkdownFences(value)));

            while (reader.Read())
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                responseCount++;
                JsonObject? json = JsonNode.Parse(document.RootElement.GetRawText())?.AsObject();
                if (!TryParseResponseObject(json, out WeatherForecastAgentResponse? candidate, out string candidateFailure))
                {
                    latestFailure = candidateFailure;
                    continue;
                }

                if (candidate!.ContentType == WeatherForecastAgentResponseContentType.AdaptiveCard)
                {
                    adaptiveCardResponse = candidate;
                }
                else
                {
                    textResponse = candidate;
                }
            }

            response = adaptiveCardResponse ?? textResponse;
            if (response != null)
            {
                return true;
            }

            validationFailure = responseCount == 0
                ? "The response did not contain a JSON value."
                : latestFailure ?? "None of the JSON values matched the required response schema.";
            return false;
        }
        catch (Exception exception)
        {
            validationFailure = $"The response could not be parsed: {exception.Message}";
            return false;
        }
    }

    private static bool TryParseResponseObject(
        JsonObject? json,
        out WeatherForecastAgentResponse? response,
        out string validationFailure)
    {
        response = null;
        validationFailure = string.Empty;
        try
        {
            if (json == null)
            {
                validationFailure = "The response is not a JSON object.";
                return false;
            }

            string? contentType = json["contentType"]?.GetValue<string>();
            JsonNode? content = json?["content"]?.DeepClone();
            WeatherForecastAgentResponseContentType? parsedType = contentType switch
            {
                "Text" => WeatherForecastAgentResponseContentType.Text,
                "AdaptiveCard" => WeatherForecastAgentResponseContentType.AdaptiveCard,
                _ => null
            };
            if (content == null || parsedType == null)
            {
                validationFailure = "The response must contain content and a contentType of Text or AdaptiveCard.";
                return false;
            }

            if (parsedType == WeatherForecastAgentResponseContentType.Text
                && (content is not JsonValue textValue || !textValue.TryGetValue(out string? _)))
            {
                validationFailure = "A Text response must contain a string content value.";
                return false;
            }

            JsonNode? normalizedContent = parsedType == WeatherForecastAgentResponseContentType.AdaptiveCard
                ? NormalizeAdaptiveCard(content, out validationFailure)
                : content;
            if (normalizedContent == null)
            {
                validationFailure = string.IsNullOrEmpty(validationFailure)
                    ? "The AdaptiveCard content is invalid."
                    : validationFailure;
                return false;
            }

            response = new WeatherForecastAgentResponse
            {
                ContentType = parsedType.Value,
                Content = normalizedContent
            };
            return true;
        }
        catch (Exception exception)
        {
            validationFailure = $"The response could not be parsed: {exception.Message}";
            return false;
        }
    }

    private static JsonNode? NormalizeAdaptiveCard(JsonNode content, out string validationFailure)
    {
        validationFailure = string.Empty;
        JsonNode? card = content;
        if (content is JsonValue value && value.TryGetValue(out string? stringContent))
        {
            card = JsonNode.Parse(RemoveMarkdownFences(stringContent ?? string.Empty));
        }

        if (card is not JsonObject cardObject
            || cardObject["type"]?.GetValue<string>() != "AdaptiveCard"
            || cardObject["version"]?.GetValue<string>() != "1.5"
            || cardObject["$schema"]?.GetValue<string>() != "http://adaptivecards.io/schemas/adaptive-card.json"
            || cardObject["body"] is not JsonArray body
            || cardObject["actions"] is not JsonArray actions)
        {
            validationFailure = "The card must include AdaptiveCard type, version 1.5, $schema, body, and actions.";
            return null;
        }

        bool hasWeatherHeading = false;
        JsonArray? facts = null;
        foreach (JsonNode? item in body)
        {
            if (item is not JsonObject bodyItem)
            {
                continue;
            }

            if (bodyItem["type"]?.GetValue<string>() == "TextBlock"
                && bodyItem["text"]?.GetValue<string>() is string heading
                && heading.StartsWith("Weather forecast for ", StringComparison.Ordinal)
                && heading["Weather forecast for ".Length..].Trim().Length > 0)
            {
                hasWeatherHeading = true;
            }

            if (bodyItem["type"]?.GetValue<string>() == "FactSet"
                && bodyItem["facts"] is JsonArray cardFacts)
            {
                facts = cardFacts;
            }
        }

        bool hasDateFact = HasFact(facts, "Date");
        bool hasTemperatureFact = HasFact(facts, "Temperature");
        bool hasMsnAction = false;
        foreach (JsonNode? item in actions)
        {
            if (item is JsonObject action
                && action["type"]?.GetValue<string>() == "Action.OpenUrl"
                && action["url"]?.GetValue<string>() is string url
                && url.StartsWith("https://www.msn.com/en-us/weather/forecast/in-", StringComparison.Ordinal))
            {
                hasMsnAction = true;
                break;
            }
        }

        if (!hasWeatherHeading || !hasDateFact || !hasTemperatureFact || !hasMsnAction)
        {
            validationFailure =
                $"The card is missing required content: weather heading={hasWeatherHeading}, date fact={hasDateFact}, temperature fact={hasTemperatureFact}, MSN action={hasMsnAction}.";
            return null;
        }

        return card;
    }

    private static bool HasFact(JsonArray? facts, string title)
    {
        if (facts == null)
        {
            return false;
        }

        foreach (JsonNode? item in facts)
        {
            if (item is JsonObject fact
                && fact["title"]?.GetValue<string>() == title
                && fact["value"]?.GetValue<string>() is string value
                && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveMarkdownFences(string value) => value
        .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("```", string.Empty, StringComparison.Ordinal)
        .Trim();

    private static void TrimHistory(ChatHistory chatHistory)
    {
        while (chatHistory.Count > MaximumHistoryMessages)
        {
            chatHistory.RemoveAt(0);
        }
    }
}

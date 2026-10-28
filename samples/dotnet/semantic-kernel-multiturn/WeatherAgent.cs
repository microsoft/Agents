// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelMultiturn.Agents;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SemanticKernelMultiturn;

public class WeatherAgent : AgentApplication
{
    private const string WelcomeMessage = "Hello and welcome! I'm here to help with all your weather forecast needs!";
    private const string ProcessingMessage = "Working on a response for you";
    private const string FailureMessage = "Sorry, I couldn't get the weather forecast at the moment.";
    private readonly Kernel _kernel;

    public WeatherAgent(AgentApplicationOptions options, Kernel kernel) : base(options)
    {
        this._kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
    }

    protected async Task MessageActivityAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        turnContext.StreamingResponse.FeedbackLoopEnabled = true;
        turnContext.StreamingResponse.EnableGeneratedByAILabel = true;
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync(ProcessingMessage, cancellationToken);

        try
        {
            string input = turnContext.Activity.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input))
            {
                turnContext.StreamingResponse.QueueTextChunk("Please enter a weather question.");
                return;
            }

            ServiceCollection serviceCollection =
            [
                new ServiceDescriptor(typeof(ITurnState), turnState),
                new ServiceDescriptor(typeof(ITurnContext), turnContext),
                new ServiceDescriptor(typeof(Kernel), this._kernel),
            ];
            using ServiceProvider services = serviceCollection.BuildServiceProvider();

            ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
            WeatherForecastAgent weatherForecastAgent = new(this._kernel, services);
            WeatherForecastAgentResponse response = await weatherForecastAgent.InvokeAgentAsync(input, chatHistory);

            if (response.ContentType == WeatherForecastAgentResponseContentType.Text)
            {
                turnContext.StreamingResponse.QueueTextChunk(
                    response.Content?.GetValue<string>() ?? FailureMessage);
            }
            else if (response.Content != null)
            {
                turnContext.StreamingResponse.FinalMessage = MessageFactory.Attachment(new Attachment
                {
                    ContentType = ContentTypes.AdaptiveCard,
                    Content = JsonSerializer.Deserialize<JsonElement>(response.Content.ToJsonString())
                });
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine($"Error during agent execution: {exception}");
            turnContext.StreamingResponse.QueueTextChunk(FailureMessage);
        }
        finally
        {
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
        }
    }

    protected async Task WelcomeMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(WelcomeMessage), cancellationToken);
            }
        }
    }
}

using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Prompty;
using Microsoft.Extensions.FileProviders;
using Microsoft.Agents.Builder;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyM365Agent1.Bot.Plugins;

namespace MyM365Agent1.Bot.Agents;

public class WeatherForecastAgent
{
    private readonly Kernel _kernel;
    private readonly KernelFunction _weatherFunction;
    private readonly DateTimePlugin _dateTimePlugin;
    private readonly IServiceProvider _serviceProvider;

    private const string AgentName = "WeatherForecastAgent";
    private const string AgentInstructions = """
        You are a friendly assistant that helps people find a weather forecast for a given time and place.
        You may ask follow up questions until you have enough information to answer the customers question,
        but once you have a forecast forecast, make sure to format it nicely using an adaptive card.
        You should use adaptive JSON format to display the information in a visually appealing way and include a button for more details that points at https://www.msn.com/en-us/weather/forecast/in-{location}
        You should use adaptive cards version 1.5 or later.

        Respond in JSON format with the following JSON schema:
        
        {
            "contentType": "'Text' or 'AdaptiveCard' only",
            "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
        }
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherForecastAgent"/> class.
    /// </summary>
    /// <param name="kernel">An instance of <see cref="Kernel"/> for interacting with an LLM.</param>
    public WeatherForecastAgent(Kernel kernel, IServiceProvider service)
    {
        _kernel = kernel;
        _serviceProvider = service;

        // Create Prompty function instead of ChatCompletionAgent
        var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
#pragma warning disable SKEXP0040 // Experimental API
        _weatherFunction = _kernel.CreateFunctionFromPromptyFile("Prompts/weather-forecast.prompty", fileProvider);
#pragma warning restore SKEXP0040

        // Create plugin instances for manual calling
        _dateTimePlugin = new DateTimePlugin();
    }

    /// <summary>
    /// Invokes the agent with the given input and returns the response.
    /// </summary>
    /// <param name="input">A message to process.</param>
    /// <returns>An instance of <see cref="WeatherForecastAgentResponse"/></returns>
    public async Task<WeatherForecastAgentResponse> InvokeAgentAsync(string input, ChatHistory chatHistory)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        try
        {
            Console.WriteLine($"[WeatherForecastAgent] Processing input: {input}");

            // Detect if this is a weather-related query
            bool isWeatherQuery = IsWeatherQuery(input);
            
            string currentDate = _dateTimePlugin.Today();
            string weatherData = "No data available";
            string location = "Unknown";

            if (isWeatherQuery)
            {
                Console.WriteLine($"[WeatherForecastAgent] Detected weather query, extracting location...");
                
                // Extract location from input (simple pattern matching)
                location = ExtractLocation(input);
                
                if (!string.IsNullOrEmpty(location))
                {
                    Console.WriteLine($"[WeatherForecastAgent] Getting weather for {location}");
                    
                    // Get weather data using our plugin (we need to create a temporary ITurnContext)
                    // For now, we'll simulate the weather data since the plugin needs ITurnContext
                    weatherData = GetWeatherData(location, currentDate);
                }
            }

            // Prepare arguments for the Prompty template
            var arguments = new KernelArguments()
            {
                ["user_request"] = input,
                ["current_date"] = currentDate,
                ["weather_data"] = weatherData,
                ["location"] = location
            };

            Console.WriteLine($"[WeatherForecastAgent] Calling Prompty function with args: {string.Join(", ", arguments.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Invoke the Prompty function
            var result = await _kernel.InvokeAsync(_weatherFunction, arguments);
            
            string response = result.ToString().Trim();
            Console.WriteLine($"[WeatherForecastAgent] Received response: {response.Substring(0, Math.Min(200, response.Length))}...");

            return ParseResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WeatherForecastAgent] Error occurred: {ex.Message}");
            Console.WriteLine($"[WeatherForecastAgent] Stack trace: {ex.StackTrace}");
            
            return new WeatherForecastAgentResponse()
            {
                Content = "I'm sorry, I encountered an error while processing your request. Please try again.",
                ContentType = WeatherForecastAgentResponseContentType.Text
            };
        }
    }

    private bool IsWeatherQuery(string input)
    {
        var weatherKeywords = new[] { "weather", "forecast", "temperature", "rain", "sunny", "cloudy", "storm", "hot", "cold" };
        return weatherKeywords.Any(keyword => input.ToLower().Contains(keyword));
    }

    private string ExtractLocation(string input)
    {
        // Simple regex to extract location patterns like "in [Location]"
        var locationPatterns = new[]
        {
            @"(?:in|for|at)\s+([A-Za-z\s]+?)(?:\s+today|\s+tomorrow|\s*\?|\s*$)",
            @"weather\s+(?:in|for|at)\s+([A-Za-z\s]+)",
            @"([A-Za-z\s]+?)\s+weather"
        };

        foreach (var pattern in locationPatterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var location = match.Groups[1].Value.Trim();
                // Filter out common words that aren't locations
                var excludeWords = new[] { "the", "today", "tomorrow", "what", "how", "is", "like" };
                if (!excludeWords.Contains(location.ToLower()) && location.Length > 1)
                {
                    return location;
                }
            }
        }

        return string.Empty;
    }

    private string GetWeatherData(string location, string date)
    {
        // Simulate weather data since we can't easily use the plugin without ITurnContext
        // In a real implementation, you'd call an actual weather API here
        var random = new Random();
        var temperature = random.Next(45, 85);
        var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Rainy", "Clear" };
        var condition = conditions[random.Next(conditions.Length)];
        var humidity = random.Next(30, 80);

        return $"Temperature: {temperature}°F, Condition: {condition}, Humidity: {humidity}%";
    }

    private WeatherForecastAgentResponse ParseResponse(string response)
    {
        try
        {
            // Extract JSON from the response
            int jsonStart = response.IndexOf('{');
            int jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var jsonNode = JsonNode.Parse(jsonContent);
                
                return new WeatherForecastAgentResponse()
                {
                    Content = jsonNode["content"]?.ToString() ?? response,
                    ContentType = Enum.Parse<WeatherForecastAgentResponseContentType>(
                        jsonNode["contentType"]?.ToString() ?? "Text", true)
                };
            }
            
            // If no JSON found, return as text
            return new WeatherForecastAgentResponse()
            {
                Content = response,
                ContentType = WeatherForecastAgentResponseContentType.Text
            };
        }
        catch (Exception)
        {
            // Fallback to plain text response
            return new WeatherForecastAgentResponse()
            {
                Content = response,
                ContentType = WeatherForecastAgentResponseContentType.Text
            };
        }
    }
}

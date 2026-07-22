// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Discord;
using Discord.WebSocket;
using DiscordAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;

// Discord host for the weather (+ WorkIQ, next step) agent.
//
// Discord has no first-party Azure Bot Service channel, so we connect directly to the
// Discord Gateway with Discord.Net and drive the Agent Framework agent ourselves:
//   Discord message -> agent.RunStreamingAsync -> reply as a Discord embed (Discord's card).

var config = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .Build();

// Bot token from user-secret "Discord:BotToken" or env var DISCORD_BOT_TOKEN.
var token = config["Discord:BotToken"] ?? Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine(
        "Missing Discord bot token. Set it with:\n" +
        "  dotnet user-secrets set \"Discord:BotToken\" \"<token>\"\n" +
        "or the DISCORD_BOT_TOKEN environment variable.");
    return;
}

// ---- Build the agent (same stack as the weather sample) ----
var endpoint = config["AIServices:AzureOpenAI:Endpoint"];
var apiKey = config["AIServices:AzureOpenAI:ApiKey"];
var deployment = config["AIServices:AzureOpenAI:DeploymentName"];
var openWeatherApiKey = config["OpenWeatherApiKey"];

if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) ||
    string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(openWeatherApiKey))
{
    Console.Error.WriteLine(
        "Missing AI/weather config. Set user-secrets:\n" +
        "  AIServices:AzureOpenAI:Endpoint, AIServices:AzureOpenAI:ApiKey, AIServices:AzureOpenAI:DeploymentName, OpenWeatherApiKey");
    return;
}

const string instructions = """
    You are a friendly feline assistant. You always speak like a cat (use "meow", playful cat puns, and emojis when they fit).

    You can help with two kinds of requests, and you must always pick the right tool:
    1. United States weather -- use your weather tools:
       - current-weather tool for current conditions (temperature, low/high, wind, humidity, short description).
       - forecast tool for the next 5 days (date, high/low, short description).
       - date tool to resolve "today". Location is a city name; resolve 2-letter US state codes to the full US state name.
    2. Anything that is NOT United States weather -- use the WorkIQ tools (Microsoft 365 / Teams: chats, channels, teams, messages).

    Routing rule: US weather questions go to the weather tools; every other question goes to the WorkIQ tools.
    Format answers nicely in markdown, keep them easy to read, and always speak like a cat. Use emojis if it fits!
    """;

IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsIChatClient();

var weatherTool = new WeatherTool(openWeatherApiKey);
var tools = new List<AITool>
{
    AIFunctionFactory.Create(DateTimeTool.GetDate),
    AIFunctionFactory.Create(weatherTool.GetCurrentWeatherForLocation),
    AIFunctionFactory.Create(weatherTool.GetWeatherForecastForLocation),
};

// ---- WorkIQ (Agent 365) MCP tools ----
// Discord has no OBO sign-in channel, so we use a dev bearer token for the Teams MCP server.
// Token from user-secret "WorkIQ:McpTeamsServerToken" or env BEARER_TOKEN_MCP_TEAMSSERVER
// (refresh with: a365 develop get-token ...). If absent, the agent runs weather-only.
var mcpToken = config["WorkIQ:McpTeamsServerToken"] ?? Environment.GetEnvironmentVariable("BEARER_TOKEN_MCP_TEAMSSERVER");
if (!string.IsNullOrWhiteSpace(mcpToken))
{
    try
    {
        var mcpTransport = new SseClientTransport(new SseClientTransportOptions
        {
            Endpoint = new Uri("https://agent365.svc.cloud.microsoft/agents/servers/mcp_TeamsServer"),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {mcpToken}" },
            TransportMode = HttpTransportMode.AutoDetect,
            Name = "mcp_TeamsServer",
        });
        var mcpClient = await McpClientFactory.CreateAsync(mcpTransport);
        var mcpTools = await mcpClient.ListToolsAsync();
        tools.AddRange(mcpTools);
        Console.WriteLine($"WorkIQ MCP tools loaded from mcp_TeamsServer: {mcpTools.Count}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"WorkIQ MCP tools failed to load (continuing weather-only): {ex.Message}");
    }
}
else
{
    Console.WriteLine("No WorkIQ MCP token (BEARER_TOKEN_MCP_TEAMSSERVER) - running weather-only.");
}

AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "Purrfect Weather Agent",
    ChatOptions = new ChatOptions
    {
        Temperature = 0.2f,
        Tools = tools,
        Instructions = instructions,
        AllowMultipleToolCalls = true,
    },
});

// One conversation session per Discord channel, so context is preserved within a channel.
var sessions = new ConcurrentDictionary<ulong, AgentSession>();

// ---- Discord wiring ----
var socketConfig = new DiscordSocketConfig
{
    // MessageContent is a privileged intent - enable it in the Discord Developer Portal
    // (Bot -> Privileged Gateway Intents -> Message Content Intent).
    GatewayIntents =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.DirectMessages |
        GatewayIntents.MessageContent,
    LogLevel = LogSeverity.Info
};

var client = new DiscordSocketClient(socketConfig);

client.Log += msg =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};

client.Ready += () =>
{
    Console.WriteLine($"Discord bot ready as {client.CurrentUser}");
    return Task.CompletedTask;
};

client.MessageReceived += async (SocketMessage message) =>
{
    // Only handle real user messages; ignore system messages and other bots (incl. ourselves).
    if (message is not SocketUserMessage userMessage) return;
    if (message.Author.IsBot) return;

    var userText = message.Content?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(userText)) return;

    try
    {
        using (userMessage.Channel.EnterTypingState())
        {
            var session = sessions.GetOrAdd(message.Channel.Id, _ => agent.CreateSessionAsync().GetAwaiter().GetResult());

            // Run the agent and collect the full answer.
            var sb = new StringBuilder();
            await foreach (var update in agent.RunStreamingAsync(userText, session))
            {
                if (update.Role == ChatRole.Assistant && !string.IsNullOrEmpty(update.Text))
                {
                    sb.Append(update.Text);
                }
            }

            var answer = sb.ToString();
            if (string.IsNullOrWhiteSpace(answer)) answer = "_(no response)_";
            if (answer.Length > 4000) answer = answer[..4000] + "…";

            // Discord embed = Discord's equivalent of a Slack Block Kit card.
            var embed = new EmbedBuilder()
                .WithColor(new Color(0x2E, 0x9B, 0xF5))
                .WithAuthor("🐾 Purrfect Assistant")
                .WithDescription(answer)
                .WithFooter("Agent Framework + WorkIQ · Discord adapter")
                .WithCurrentTimestamp()
                .Build();

            await userMessage.Channel.SendMessageAsync(embed: embed);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error handling message: {ex}");
        await userMessage.Channel.SendMessageAsync("Sorry, I hit an error while processing that. 🐾");
    }
};

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

Console.WriteLine("Discord agent host started. Press Ctrl+C to exit.");

// Keep the process running.
await Task.Delay(Timeout.Infinite);

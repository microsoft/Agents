// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentFrameworkWeather.Adapters;
using AgentFrameworkWeather.Tools;
using Microsoft.Agents.A365.Runtime.Utils;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AgentFrameworkWeather.Agent
{
    [SlackExtension]
    public partial class WeatherAgent : AgentApplication
    {
        private readonly string AgentWelcomeMessage = "Hello! I'm your friendly purr-ductivity cat assistant. I can fetch the current weather or forecast for any US city, and I can help with your Microsoft 365 work too - like Teams chats, mail, and files. Ask me a weather question (city + 2-letter state) or anything about your workday. Meow!";

        private readonly string AgentInstructions = """
        You are a friendly feline assistant. You always speak like a cat (use "meow", playful cat puns, and emojis when they fit).

        You can help with two kinds of requests, and you must always pick the right tool for each:

        1. Weather in the United States -- use your local weather tools:
           - Use {{WeatherLookupTool.GetCurrentWeatherForLocation}} for current conditions. Include the current temperature, low and high temperatures, wind speed, humidity, and a short description of the weather.
           - Use {{WeatherLookupTool.GetWeatherForecastForLocation}} for forecasts. Report the next 5 days, including the current day, with the date, high and low temperatures, and a short description.
           - Use {{DateTimeFunctionTool.getDate}} to get the current date and time.
           - Location is a city name; resolve 2-letter US state codes to the full name of the United States state.

        2. Anything that is NOT United States weather -- use the WorkIQ tools to answer. This includes Microsoft 365 and Microsoft Teams tasks such as reading or posting chat messages, listing chats, channels, and teams, and other workplace questions.

        Routing rule: US weather questions go to the weather tools; every other question goes to the WorkIQ tools. You may ask brief follow-up questions when you need more detail. Always format answers nicely in markdown, keep them easy to read, and always speak like a cat. Use emojis if it fits the response!
        """;

        private readonly IChatClient? _chatClient = null;
        private readonly IConfiguration? _configuration = null;
        private readonly ILogger<WeatherAgent>? _logger = null;

        // WorkIQ (Agent 365) MCP tool service. Nullable so the agent still runs
        // (weather-only) when the service or its configuration is unavailable.
        private readonly IMcpToolRegistrationService? _toolService = null;

        // Cache of WorkIQ MCP tools keyed by access-token hash. Loading the tools is an HTTP
        // round-trip to mcp_TeamsServer (~5s); the tool set is stable for the life of a token,
        // so we reuse it across turns and only reload when the token changes or nears expiry.
        private static readonly ConcurrentDictionary<string, CachedMcpTools> _mcpToolsCache = new();

        private sealed record CachedMcpTools(IList<AITool> Tools, DateTimeOffset ExpiresAt);

        // Auth handler names for MCP access (configurable via appsettings.json).
        private readonly string? AgenticAuthHandlerName;
        private readonly string? OboAuthHandlerName;

        public WeatherAgent(
            AgentApplicationOptions options,
            IChatClient chatClient,
            IConfiguration configuration,
            IMcpToolRegistrationService? toolService = null,
            ILogger<WeatherAgent>? logger = null) : base(options)
        {
            _chatClient = chatClient;
            _configuration = configuration;
            _toolService = toolService;
            _logger = logger;

            // Read auth handler names from configuration (can be empty/null to disable).
            AgenticAuthHandlerName = _configuration.GetValue<string>("AgentApplication:AgenticAuthHandlerName");
            OboAuthHandlerName = _configuration.GetValue<string>("AgentApplication:UserAuthorization:Handlers:mcs:Settings:AzureBotOAuthConnectionName");

            // Greet when members are added to the conversation
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            // Background warm-up: an Event activity named "warmup" pre-loads the WorkIQ MCP tools
            // into the shared cache (e.g. sent by the Discord host at startup) so the first real
            // message is fast. Registered before the generic message handler.
            OnActivity(ActivityTypes.Event, OnWarmupAsync);

            OnMessage("ForceLogout", OnLogout);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageAsync, autoSignInHandlers: [OboAuthHandlerName]);
        }

        private async Task OnLogout(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState);
        }

        /// <summary>
        /// Handle the "warmup" Event activity: pre-load WorkIQ MCP tools into the cache and return
        /// without replying. Ignores any other Event activity.
        /// </summary>
        private async Task OnWarmupAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            if (!string.Equals(turnContext.Activity.Name, "warmup", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _logger?.LogInformation("Warm-up: pre-loading WorkIQ MCP tools for channel {Channel}.", turnContext.Activity.ChannelId);
            await WarmUpWorkIqToolsAsync(turnContext).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a bearer token is available in the environment for development/testing.
        /// </summary>
        private static bool TryGetBearerTokenForDevelopment(out string? bearerToken)
        {
            bearerToken = Environment.GetEnvironmentVariable("BEARER_TOKEN");
            return !string.IsNullOrEmpty(bearerToken);
        }

        /// <summary>
        /// Graceful fallback to weather-only mode when MCP tools fail to load.
        /// Only allowed in Development AND when SKIP_TOOLING_ON_ERRORS=true.
        /// </summary>
        private static bool ShouldSkipToolingOnErrors()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Production";
            var skip = Environment.GetEnvironmentVariable("SKIP_TOOLING_ON_ERRORS");
            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrEmpty(skip)
                   && skip.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(AgentWelcomeMessage);
                }
            }
        }
        protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Slack: render the response as native Slack Blocks (a card) instead of the
            // streamed Bot Framework text. Other channels keep the streaming experience.
            bool isSlack = turnContext.Activity.ChannelId == Channels.Slack;
            bool isDiscord = turnContext.Activity.ChannelId == DiscordAdapter.ChannelId;

            var userToken = await UserAuthorization.GetTurnTokenAsync(turnContext, OboAuthHandlerName);

            var userText = turnContext.Activity.Text?.Trim() ?? string.Empty;

            // Pick the auth handler for this turn (agentic vs OBO). In dev the BEARER_TOKEN path is used.
            // Discord signs the user in (custom DiscordAdapter provides the IUserTokenClient), but the
            // WorkIQ tools still use the dev bearer-token path here; wiring the tools to the user's OBO
            // token is a follow-up.
            string? toolAuthHandlerName = isDiscord
                ? null
                : turnContext.Activity.IsAgenticRequest()
                    ? AgenticAuthHandlerName
                    : OboAuthHandlerName;

            var _agent = await GetClientAgent(turnContext, turnState, toolAuthHandlerName);

            // Read or Create the conversation thread for this conversation.
            AgentSession? thread = await GetConversationThread(_agent, turnState);

            if (isSlack)
            {
                // Collect the full response, then post it as a Slack Block card.
                var sb = new StringBuilder();
                await foreach (var response in _agent.RunStreamingAsync(userText, thread, cancellationToken: cancellationToken))
                {
                    if (response.Role == ChatRole.Assistant && !string.IsNullOrEmpty(response.Text))
                    {
                        sb.Append(response.Text);
                    }
                }
                turnState.Conversation.SetValue("conversation.threadInfo", (await _agent.SerializeSessionAsync(thread)).ToString());
                await PostSlackBlocksAsync(turnContext, sb.ToString(), cancellationToken);
                return;
            }

            // Discord: collect the full response and send it as ONE message activity; the
            // DiscordAdapter renders it as an embed. (Streaming would create many partial messages.)
            if (isDiscord)
            {
                var sb = new StringBuilder();
                await foreach (var response in _agent.RunStreamingAsync(userText, thread, cancellationToken: cancellationToken))
                {
                    if (response.Role == ChatRole.Assistant && !string.IsNullOrEmpty(response.Text))
                    {
                        sb.Append(response.Text);
                    }
                }
                turnState.Conversation.SetValue("conversation.threadInfo", (await _agent.SerializeSessionAsync(thread)).ToString());

                var answer = sb.ToString();
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(answer), cancellationToken).ConfigureAwait(false);
                }
                return;
            }

            // Non-Slack channels: stream the response back as it is produced.
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Just a moment please..").ConfigureAwait(false);
            try
            {
                await foreach (var response in _agent.RunStreamingAsync(userText, thread, cancellationToken: cancellationToken))
                {
                    if (response.Role == ChatRole.Assistant && !string.IsNullOrEmpty(response.Text))
                    {
                        turnContext.StreamingResponse.QueueTextChunk(response.Text);
                    }
                }
                turnState.Conversation.SetValue("conversation.threadInfo", (await _agent.SerializeSessionAsync(thread)).ToString());
            }
            finally
            {
                await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false); // End the streaming response
            }
        }

        /// <summary>
        /// Post the agent's answer to Slack as a Block Kit card (mrkdwn section) via the Slack API,
        /// instead of the plain text that the Azure Bot Slack channel would render.
        /// </summary>
        private async Task PostSlackBlocksAsync(ITurnContext turnContext, string text, CancellationToken cancellationToken)
        {
            var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();

            // Slack section text is limited to 3000 chars; trim defensively.
            var body = string.IsNullOrWhiteSpace(text) ? "_(no response)_" : text;
            if (body.Length > 2900)
            {
                body = body.Substring(0, 2900) + "…";
            }

            // JSON-encode the text (adds surrounding quotes + escaping) so it is safe inside the payload.
            var encoded = JsonSerializer.Serialize(body);

            // Wrap the blocks in a message attachment with a blue accent color. Slack Block Kit
            // "header" blocks are plain_text only (no colored text), so the blue vertical accent
            // bar on the attachment is the standard way to give the card a blue "brand" color.
            var message = $$"""
            {
                "channel": "{{channelData.Channel}}",
                "attachments": [
                    {
                        "color": "#2E9BF5",
                        "blocks": [
                            {
                                "type": "header",
                                "text": { "type": "plain_text", "text": "🐾 Purrfect Assistant", "emoji": true }
                            },
                            {
                                "type": "section",
                                "text": { "type": "mrkdwn", "text": {{encoded}} }
                            },
                            { "type": "divider" },
                            {
                                "type": "context",
                                "elements": [
                                    { "type": "mrkdwn", "text": ":robot_face: *Agent Framework* + :sparkles: *WorkIQ*  |  rendered with Slack Block Kit" }
                                ]
                            }
                        ]
                    }
                ]
            }
            """;

            await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
        }



        /// <summary>
        /// Resolve the ChatClientAgent with tools and options for this turn operation. 
        /// This will use the IChatClient registered in DI.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task<AIAgent> GetClientAgent(ITurnContext context, ITurnState turnState, string? authHandlerName)
        {
            AssertionHelpers.ThrowIfNull(_configuration!, nameof(_configuration));
            AssertionHelpers.ThrowIfNull(context, nameof(context));
            AssertionHelpers.ThrowIfNull(_chatClient!, nameof(_chatClient));

            // Setup the local (weather) tools - these are always available.
            WeatherLookupTool weatherLookupTool = new(context, _configuration!);
            var toolList = new List<AITool>
            {
                AIFunctionFactory.Create(DateTimeFunctionTool.getDate),
                AIFunctionFactory.Create(weatherLookupTool.GetCurrentWeatherForLocation),
                AIFunctionFactory.Create(weatherLookupTool.GetWeatherForecastForLocation)
            };

            // Attach the WorkIQ MCP tools (token-cached) on top of the weather tools.
            toolList.AddRange(await GetWorkIqMcpToolsAsync(context, authHandlerName, announceLoading: true).ConfigureAwait(false));

            // Setup the tools for the agent:
            var toolOptions = new ChatOptions
            {
                Temperature = (float?)0.2,
                Tools = toolList,
                Instructions = AgentInstructions,
                AllowMultipleToolCalls = true
            };

            // Create the chat Client passing in agent instructions and tools: 
            return new ChatClientAgent(_chatClient!,
                    new ChatClientAgentOptions
                    {
                        Name = "Purrfect Weather Agent",
                        ChatOptions = toolOptions,
                        ChatHistoryProvider =
#pragma warning disable MEAI001 // MessageCountingChatReducer is for evaluation purposes only and is subject to change or removal in future updates
                            new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
                            {
                                JsonSerializerOptions = ProtocolJsonSerializer.SerializationOptions,
                                ChatReducer = new MessageCountingChatReducer(10)
                            })
#pragma warning restore MEAI001 // MessageCountingChatReducer is for evaluation purposes only and is subject to change or removal in future updates

                    })
                .AsBuilder()
                .UseOpenTelemetry(sourceName: AgentsTelemetry.SourceName, (cfg) => cfg.EnableSensitiveData = true)
                .Build(); 
        }

        /// <summary>
        /// Resolve the WorkIQ MCP tools for this operation, using the token-keyed cache. Returns an
        /// empty list when auth or tools are unavailable. When <paramref name="announceLoading"/> is
        /// true, a "Loading tools..." status is streamed on a cache miss (skipped for background warm-up).
        /// </summary>
        private async Task<IList<AITool>> GetWorkIqMcpToolsAsync(ITurnContext context, string? authHandlerName, bool announceLoading)
        {
            // Acquire the access token once - used for WorkIQ MCP tool loading.
            string? accessToken = null;
            string? agentId = null;
            if (!string.IsNullOrEmpty(authHandlerName))
            {
                // Production / Teams: exchange an OBO token via the auth handler.
                accessToken = await UserAuthorization.GetTurnTokenAsync(context, authHandlerName);
                agentId = Utility.ResolveAgentIdentity(context, accessToken);
            }
            else if (TryGetBearerTokenForDevelopment(out var bearerToken))
            {
                // Local dev: use the bearer token from `a365 develop get-token`.
                _logger?.LogInformation("Using bearer token from environment for WorkIQ MCP.");
                accessToken = bearerToken;
                agentId = Utility.ResolveAgentIdentity(context, accessToken!);
            }
            else
            {
                _logger?.LogWarning("No auth handler or bearer token - WorkIQ MCP tools will not be loaded (weather-only).");
            }

            if (_toolService == null || string.IsNullOrEmpty(agentId))
            {
                return Array.Empty<AITool>();
            }

            try
            {
                // The tool set is bound to the user's access token; cache it by token so we
                // only pay the ~5s MCP load once per token instead of on every turn.
                var cacheKey = HashToken(accessToken!);
                if (_mcpToolsCache.TryGetValue(cacheKey, out var cachedTools)
                    && cachedTools.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    _logger?.LogInformation("WorkIQ MCP tools served from cache ({Count} tools).", cachedTools.Tools.Count);
                    return cachedTools.Tools;
                }

                if (announceLoading)
                {
                    await context.StreamingResponse.QueueInformativeUpdateAsync("Loading tools...");
                }

                // For the bearer-token (dev) flow, pass the token as an override and
                // use the OBO/agentic handler name if configured.
                var handlerForMcp = !string.IsNullOrEmpty(authHandlerName)
                    ? authHandlerName
                    : OboAuthHandlerName ?? AgenticAuthHandlerName ?? string.Empty;
                var tokenOverride = string.IsNullOrEmpty(authHandlerName) ? accessToken : null;

                var a365Tools = await _toolService.GetMcpToolsAsync(agentId, UserAuthorization, handlerForMcp, context, tokenOverride).ConfigureAwait(false);
                if (a365Tools != null && a365Tools.Count > 0)
                {
                    // Expire the cache entry shortly before the token itself expires so we
                    // never invoke a cached tool with a dead token.
                    var expiresAt = GetTokenExpiry(accessToken!).AddMinutes(-1);
                    _mcpToolsCache[cacheKey] = new CachedMcpTools(a365Tools, expiresAt);
                    PruneExpiredMcpToolsCache();
                    _logger?.LogInformation("WorkIQ MCP tools loaded from server ({Count} tools); cached until {Expiry:u}.", a365Tools.Count, expiresAt);
                    return a365Tools;
                }

                return Array.Empty<AITool>();
            }
            catch (Exception ex)
            {
                // If setup fails, keep serving weather instead of crashing.
                if (ShouldSkipToolingOnErrors())
                {
                    _logger?.LogWarning(ex, "Failed to register WorkIQ MCP tools. Continuing weather-only (SKIP_TOOLING_ON_ERRORS=true).");
                    return Array.Empty<AITool>();
                }

                _logger?.LogError(ex, "Failed to register WorkIQ MCP tools.");
                throw;
            }
        }

        /// <summary>
        /// Pre-load the WorkIQ MCP tools into the shared cache so the first real message does not pay
        /// the ~5s load. Intended for background warm-up (e.g. Discord at startup) using the dev bearer
        /// token. No-op if tools are already cached or no token is available; never sends a reply.
        /// </summary>
        private Task WarmUpWorkIqToolsAsync(ITurnContext context)
            => GetWorkIqMcpToolsAsync(context, authHandlerName: null, announceLoading: false);

        /// <summary>
        /// Extract the expiry (exp claim) from a JWT access token. Falls back to a short window
        /// if the token cannot be parsed, so a bad token never yields a long-lived cache entry.
        /// </summary>
        private static DateTimeOffset GetTokenExpiry(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length >= 2)
                {
                    var payload = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp))
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(exp);
                    }
                }
            }
            catch
            {
                // Ignore parse failures and use the fallback below.
            }
            return DateTimeOffset.UtcNow.AddMinutes(5);
        }

        /// <summary>Stable, non-reversible cache key for an access token.</summary>
        private static string HashToken(string token)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }

        /// <summary>Drop expired MCP tool cache entries to bound memory as tokens rotate.</summary>
        private static void PruneExpiredMcpToolsCache()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in _mcpToolsCache)
            {
                if (entry.Value.ExpiresAt <= now)
                {
                    _mcpToolsCache.TryRemove(entry.Key, out _);
                }
            }
        }

        /// <summary>
        /// Manage Agent threads against the conversation state.
        /// </summary>
        /// <param name="agent">ChatAgent</param>
        /// <param name="turnState">State Manager for the Agent.</param>
        /// <returns></returns>
        private static async Task<AgentSession> GetConversationThread(AIAgent? agent, ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(agent);
            AgentSession thread;
            string? agentThreadInfo = turnState.Conversation.GetValue<string?>("conversation.threadInfo", () => null);
            if (string.IsNullOrEmpty(agentThreadInfo))
            {
                thread = await agent.CreateSessionAsync();
            }
            else
            {
                JsonElement ele = ProtocolJsonSerializer.ToObject<JsonElement>(agentThreadInfo);
                thread = await agent.DeserializeSessionAsync(ele);
            }
            return thread;
        }
    }
}
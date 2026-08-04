// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Discord;
using Discord.WebSocket;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentFrameworkWeather.Adapters
{
    /// <summary>
    /// Drives the <see cref="DiscordAdapter"/> from the Discord gateway.
    ///
    /// Discord is WebSocket/event driven (not HTTP), so instead of an /api/messages endpoint we
    /// subscribe to the gateway's MessageReceived event, translate each message into an Activity,
    /// and call <see cref="DiscordAdapter.ProcessActivityAsync"/> to run the shared WeatherAgent.
    ///
    /// Only starts when a Discord bot token is configured ("Discord:BotToken" or DISCORD_BOT_TOKEN).
    /// </summary>
    public class DiscordGatewayService(
        IConfiguration configuration,
        DiscordAdapter adapter,
        IServiceProvider services,
        ILogger<DiscordGatewayService> logger) : BackgroundService
    {
        private DiscordSocketClient? _client;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var token = configuration["Discord:BotToken"]
                        ?? Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogInformation("Discord bot token not configured - Discord channel disabled.");
                return;
            }

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents =
                    GatewayIntents.Guilds |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.DirectMessages |
                    GatewayIntents.MessageContent,
                LogLevel = LogSeverity.Info
            });

            _client.Log += msg =>
            {
                logger.LogInformation("[Discord] {Message}", msg.ToString());
                return Task.CompletedTask;
            };

            _client.Ready += () =>
            {
                logger.LogInformation("Discord bot ready as {User}", _client.CurrentUser);
                // Pre-load WorkIQ MCP tools in the background so the first real message is fast.
                _ = Task.Run(WarmUpToolsAsync);
                return Task.CompletedTask;
            };

            // Offload processing to a background task so a slow agent turn (several seconds while
            // WorkIQ MCP tools load and the model runs) does not block the Discord gateway heartbeat.
            // Discord.Net warns "A MessageReceived handler is blocking the gateway task" otherwise.
            _client.MessageReceived += msg =>
            {
                _ = Task.Run(() => OnDiscordMessageAsync(msg));
                return Task.CompletedTask;
            };

            await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            // Keep the service alive until shutdown.
            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }).ConfigureAwait(false);
        }

        /// <summary>
        /// Pre-load the WorkIQ MCP tools at startup so the first real Discord message is fast. Sends a
        /// synthetic "warmup" Event activity through the adapter pipeline (which builds a full TurnContext)
        /// and lets the agent populate its shared tool cache. Best-effort: failures fall back to lazy load.
        /// </summary>
        private async Task WarmUpToolsAsync()
        {
            try
            {
                var activity = new Activity
                {
                    Type = ActivityTypes.Event,
                    Name = "warmup",
                    ChannelId = DiscordAdapter.ChannelId,
                    ServiceUrl = "discord",
                    Conversation = new ConversationAccount { Id = "warmup" },
                    From = new ChannelAccount { Id = "warmup" },
                };

                using var scope = services.CreateScope();
                var agent = scope.ServiceProvider.GetRequiredService<AgentFrameworkWeather.Agent.WeatherAgent>();
                await adapter.ProcessActivityAsync(CreateBotClaimsIdentity(), activity, agent.OnTurnAsync, CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation("Discord: WorkIQ MCP tools warm-up complete.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discord: WorkIQ MCP tools warm-up failed (tools will load on first message).");
            }
        }

        private async Task OnDiscordMessageAsync(SocketMessage message)
        {
            if (message is not SocketUserMessage userMessage) return;
            if (message.Author.IsBot) return;

            var client = _client;
            if (client is null) return;

            var text = message.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text)) return;

            var conversationId = message.Channel.Id.ToString();

            // Tell the adapter which live Discord channel this conversation maps to (for replies).
            adapter.RegisterChannel(conversationId, message.Channel);

            // Translate the Discord message into an Activity the agent understands.
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Id = message.Id.ToString(),
                Text = text,
                ChannelId = DiscordAdapter.ChannelId,
                ServiceUrl = "discord",
                Conversation = new ConversationAccount { Id = conversationId },
                From = new ChannelAccount { Id = message.Author.Id.ToString(), Name = message.Author.Username },
                Recipient = new ChannelAccount { Id = client.CurrentUser.Id.ToString(), Name = client.CurrentUser.Username },
            };

            var identity = CreateBotClaimsIdentity();

            try
            {
                using (message.Channel.EnterTypingState())
                using (var scope = services.CreateScope())
                {
                    var agent = scope.ServiceProvider.GetRequiredService<AgentFrameworkWeather.Agent.WeatherAgent>();
                    await adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Discord message");
                await message.Channel.SendMessageAsync("Sorry, I hit an error while processing that. 🐾")
                    .ConfigureAwait(false);
            }
        }

        // Build a non-anonymous ClaimsIdentity carrying the Azure Bot's app id so the adapter can
        // create an IUserTokenClient (the OAuth sign-in flow needs the bot app id to call the
        // Bot Framework Token Service). Falls back to an empty identity if not configured.
        private ClaimsIdentity CreateBotClaimsIdentity()
        {
            var botAppId = configuration["Connections:BotServiceConnection:Settings:ClientId"];
            return string.IsNullOrEmpty(botAppId)
                ? new ClaimsIdentity()
                : AgentClaims.CreateIdentity(botAppId, appId: botAppId);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_client != null)
            {
                await _client.StopAsync().ConfigureAwait(false);
                await _client.LogoutAsync().ConfigureAwait(false);
            }
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

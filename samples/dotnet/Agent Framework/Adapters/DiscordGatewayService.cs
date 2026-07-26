// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Discord;
using Discord.WebSocket;
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
        private DiscordSocketClient _client;

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
                return Task.CompletedTask;
            };

            _client.MessageReceived += OnDiscordMessageAsync;

            await _client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            // Keep the service alive until shutdown.
            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }).ConfigureAwait(false);
        }

        private async Task OnDiscordMessageAsync(SocketMessage message)
        {
            if (message is not SocketUserMessage userMessage) return;
            if (message.Author.IsBot) return;

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
                Recipient = new ChannelAccount { Id = _client.CurrentUser.Id.ToString(), Name = _client.CurrentUser.Username },
            };

            var identity = new ClaimsIdentity();

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

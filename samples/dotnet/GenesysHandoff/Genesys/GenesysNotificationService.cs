// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Background service that maintains a WebSocket connection to the Genesys Cloud notification service.
    /// Subscribes to <c>v2.detail.events.conversation.{id}.user.end</c> topics to detect when a live agent
    /// disconnects, and proactively notifies the Teams user so the conversation can return to the bot.
    /// </summary>
    public class GenesysNotificationService : BackgroundService
    {
        private const string NotificationChannelApiPath = "/api/v2/notifications/channels";
        private const string SubscriptionsApiPathTemplate = "/api/v2/notifications/channels/{0}/subscriptions";
        private const string ConversationTopicPrefix = "v2.detail.events.conversation.";
        private const string UserEndTopicSuffix = ".user.end";
        private const string AgentDisconnectedStoragePrefix = "agent_disconnected_";

        private readonly IGenesysConnectionSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IStorage _storage;
        private readonly IChannelAdapter _channelAdapter;
        private readonly GenesysTokenProvider _tokenProvider;
        private readonly ConversationMappingStore _mappingStore;
        private readonly ILogger<GenesysNotificationService> _logger;

        private string? _channelId;
        private ClientWebSocket? _webSocket;

        public GenesysNotificationService(
            IGenesysConnectionSettings settings,
            IHttpClientFactory httpClientFactory,
            IStorage storage,
            IChannelAdapter channelAdapter,
            GenesysTokenProvider tokenProvider,
            ConversationMappingStore mappingStore,
            ILogger<GenesysNotificationService> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _channelAdapter = channelAdapter ?? throw new ArgumentNullException(nameof(channelAdapter));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _mappingStore = mappingStore ?? throw new ArgumentNullException(nameof(mappingStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Subscribes to the <c>v2.detail.events.conversation.{genesysConversationId}.user.end</c> topic for the given conversation.
        /// </summary>
        /// <param name="genesysConversationId">The Genesys conversation ID.</param>
        /// <param name="mcsConversationId">The MCS conversation ID used to look up the stored <see cref="ConversationReference"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task SubscribeToConversationEventsAsync(string genesysConversationId, string mcsConversationId, CancellationToken cancellationToken)
        {
            await _mappingStore.AddAsync(genesysConversationId, mcsConversationId, cancellationToken);

            if (string.IsNullOrEmpty(_channelId))
            {
                _logger.LogWarning("Notification channel not yet established. Subscription for conversation {ConversationId} will be attempted when the channel is ready.", genesysConversationId);
                return;
            }

            var topic = $"{ConversationTopicPrefix}{genesysConversationId}{UserEndTopicSuffix}";
            try
            {
                await SubscribeToTopicsAsync([topic], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic for Genesys conversation {ConversationId}. Subscription will be retried on reconnect.", genesysConversationId);
            }
        }

        /// <summary>
        /// Removes the conversation mapping for the given Genesys conversation ID.
        /// The WebSocket topic subscription will naturally become irrelevant once the conversation ends in Genesys.
        /// </summary>
        /// <param name="genesysConversationId">The Genesys conversation ID to unsubscribe.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task UnsubscribeFromConversationEventsAsync(string genesysConversationId, CancellationToken cancellationToken)
        {
            await _mappingStore.RemoveAsync(genesysConversationId, cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait briefly to let the rest of the app start up
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndListenAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Genesys notification WebSocket connection failed. Reconnecting in 15 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
            }
        }

        private async Task ConnectAndListenAsync(CancellationToken cancellationToken)
        {
            // Load persisted conversation mappings before connecting
            await _mappingStore.LoadAsync(cancellationToken);

            var authToken = await _tokenProvider.GetTokenAsync(cancellationToken);

            // Step 1: Create a notification channel
            var (channelId, connectUri) = await CreateNotificationChannelAsync(authToken, cancellationToken);
            _channelId = channelId;

            _logger.LogInformation("Genesys notification channel created: {ChannelId}", channelId);

            // Step 2: Subscribe to topics for any conversations already in the map (e.g., after reconnect)
            await ResubscribeExistingConversationsAsync(cancellationToken);

            // Step 3: Connect WebSocket
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri(connectUri), cancellationToken);

            _logger.LogInformation("Connected to Genesys notification WebSocket.");

            // Step 4: Listen for events
            await ListenForEventsAsync(cancellationToken);
        }

        private async Task ListenForEventsAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                int totalBytes = 0;
                byte[]? largeBuffer = null;

                try
                {
                    // Read the first chunk
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    totalBytes = result.Count;

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Genesys WebSocket closed by server.");
                        break;
                    }

                    // Handle multi-chunk messages
                    if (!result.EndOfMessage)
                    {
                        largeBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 4);
                        Buffer.BlockCopy(buffer, 0, largeBuffer, 0, totalBytes);

                        while (!result.EndOfMessage)
                        {
                            if (totalBytes >= largeBuffer.Length - buffer.Length)
                            {
                                var newBuffer = ArrayPool<byte>.Shared.Rent(largeBuffer.Length * 2);
                                Buffer.BlockCopy(largeBuffer, 0, newBuffer, 0, totalBytes);
                                ArrayPool<byte>.Shared.Return(largeBuffer);
                                largeBuffer = newBuffer;
                            }

                            result = await _webSocket.ReceiveAsync(
                                new ArraySegment<byte>(largeBuffer, totalBytes, largeBuffer.Length - totalBytes),
                                cancellationToken);
                            totalBytes += result.Count;
                        }
                    }

                    var messageBytes = largeBuffer != null
                        ? new ReadOnlyMemory<byte>(largeBuffer, 0, totalBytes)
                        : new ReadOnlyMemory<byte>(buffer, 0, totalBytes);

                    await ProcessNotificationMessageAsync(messageBytes, cancellationToken);
                }
                finally
                {
                    if (largeBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(largeBuffer);
                    }
                }
            }
        }

        private async Task ProcessNotificationMessageAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken)
        {
            try
            {
                using var doc = JsonDocument.Parse(messageBytes);
                var root = doc.RootElement;

                if (!root.TryGetProperty("topicName", out var topicNameElement))
                {
                    return;
                }

                var topicName = topicNameElement.GetString();
                if (string.IsNullOrEmpty(topicName))
                {
                    return;
                }

                // Handle system events
                if (topicName == "channel.metadata")
                {
                    // Heartbeat pong — no action needed
                    return;
                }

                if (topicName == "v2.system.socket_closing")
                {
                    _logger.LogInformation("Genesys WebSocket closing for maintenance. Will reconnect.");
                    // Close gracefully to trigger reconnect in the outer loop
                    if (_webSocket?.State == WebSocketState.Open)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server maintenance", cancellationToken);
                    }
                    return;
                }

                // Handle conversation user end events
                if (topicName.StartsWith(ConversationTopicPrefix, StringComparison.Ordinal)
                    && topicName.EndsWith(UserEndTopicSuffix, StringComparison.Ordinal))
                {
                    var genesysConversationId = topicName
                        .Substring(ConversationTopicPrefix.Length,
                            topicName.Length - ConversationTopicPrefix.Length - UserEndTopicSuffix.Length);

                    _logger.LogInformation("Agent disconnect detected for Genesys conversation {ConversationId}.", genesysConversationId);
                    await HandleAgentDisconnectAsync(genesysConversationId, cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Genesys notification message.");
            }
        }

        private async Task HandleAgentDisconnectAsync(string genesysConversationId, CancellationToken cancellationToken)
        {
            var mcsConversationId = await _mappingStore.RemoveAsync(genesysConversationId, cancellationToken);
            if (mcsConversationId == null)
            {
                _logger.LogWarning("No MCS conversation mapping found for Genesys conversation {ConversationId}. User may have already reset.", genesysConversationId);
                return;
            }

            try
            {
                // Look up the stored user channel reference to send a proactive message
                var state = await _storage.ReadAsync([mcsConversationId], cancellationToken);
                if (!state.TryGetValue(mcsConversationId, out var referenceObj) || referenceObj is not ConversationReference userChannelReference)
                {
                    _logger.LogWarning("No user channel reference found for MCS conversation {ConversationId}.", mcsConversationId);
                    return;
                }

                // Store a flag so the next user turn knows the agent disconnected and can clear IsEscalated
                await _storage.WriteAsync(
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { $"{AgentDisconnectedStoragePrefix}{mcsConversationId}", new { disconnected = true } }
                    },
                    cancellationToken);

                // Proactively notify the Teams user
                var continuationActivity = userChannelReference.GetContinuationActivity();
                var claimsIdentity = AgentClaims.CreateIdentity(userChannelReference.Agent.Id);

                await _channelAdapter.ProcessProactiveAsync(
                    claimsIdentity: claimsIdentity,
                    continuationActivity: continuationActivity,
                    audience: string.Empty,
                    callback: async (turnContext, ct) =>
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("The live agent has left the conversation. You are now back with the bot."),
                            cancellationToken: ct);
                    },
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Notified user about agent disconnect for MCS conversation {ConversationId}.", mcsConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle agent disconnect for MCS conversation {ConversationId}. The disconnect flag may not have been set.", mcsConversationId);
            }
        }

        /// <summary>
        /// Checks if the agent has disconnected for a given conversation and clears the flag.
        /// Called during a user turn to determine if escalation should be auto-cleared.
        /// </summary>
        /// <param name="mcsConversationId">The MCS conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><c>true</c> if the agent disconnected flag was set and cleared; <c>false</c> otherwise.</returns>
        public async Task<bool> CheckAndClearAgentDisconnectedAsync(string mcsConversationId, CancellationToken cancellationToken)
        {
            var key = $"{AgentDisconnectedStoragePrefix}{mcsConversationId}";
            var state = await _storage.ReadAsync([key], cancellationToken);

            if (state.TryGetValue(key, out _))
            {
                await _storage.DeleteAsync([key], cancellationToken);
                return true;
            }

            return false;
        }

        #region Genesys Notification API

        private async Task<(string channelId, string connectUri)> CreateNotificationChannelAsync(string authToken, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            var url = $"{_settings.ApiUrl}{NotificationChannelApiPath}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", authToken);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            var channelId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Notification channel response missing 'id'.");
            var connectUri = doc.RootElement.GetProperty("connectUri").GetString()
                ?? throw new InvalidOperationException("Notification channel response missing 'connectUri'.");

            return (channelId, connectUri);
        }

        private async Task SubscribeToTopicsAsync(string[] topics, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_channelId))
            {
                return;
            }

            var authToken = await _tokenProvider.GetTokenAsync(cancellationToken);

            using var client = _httpClientFactory.CreateClient();
            var url = $"{_settings.ApiUrl}{string.Format(SubscriptionsApiPathTemplate, _channelId)}";

            var body = new object[topics.Length];
            for (int i = 0; i < topics.Length; i++)
            {
                body[i] = new { id = topics[i] };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", authToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to subscribe to Genesys topics. Status: {StatusCode}. Response: {Response}",
                    response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Subscribed to Genesys topics: {Topics}", string.Join(", ", topics));
            }
        }

        private async Task ResubscribeExistingConversationsAsync(CancellationToken cancellationToken)
        {
            if (_mappingStore.IsEmpty)
            {
                return;
            }

            var genesysIds = _mappingStore.GetAllGenesysConversationIds();
            var topics = new string[genesysIds.Count];
            int i = 0;
            foreach (var id in genesysIds)
            {
                topics[i++] = $"{ConversationTopicPrefix}{id}{UserEndTopicSuffix}";
            }

            await SubscribeToTopicsAsync(topics, cancellationToken);
        }

        #endregion

        public override void Dispose()
        {
            _webSocket?.Dispose();
            base.Dispose();
        }
    }
}

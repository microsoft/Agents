// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Sends outbound messages to the Genesys Cloud Open Messaging API and manages
    /// the stored <see cref="ConversationReference"/> used for proactive replies.
    /// </summary>
    public class GenesysMessageSender(IGenesysConnectionSettings setting, IHttpClientFactory httpClientFactory, IStorage storage, GenesysTokenProvider tokenProvider, ILogger<GenesysMessageSender> logger)
    {
        private readonly IGenesysConnectionSettings _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        private readonly GenesysTokenProvider _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        private readonly ILogger<GenesysMessageSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private const string BearerScheme = "bearer";
        private const string ApplicationJsonMediaType = "application/json";

        /// <summary>
        /// Stores the user channel reference and sends the activity to Genesys.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <param name="mcsConversationId">The MCS conversation ID used as the sender identity.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="prefetchConversationId">When <c>true</c>, appends <c>?prefetchConversationId=true</c> and returns the Genesys conversation ID.</param>
        /// <returns>The Genesys conversation ID when <paramref name="prefetchConversationId"/> is <c>true</c>; otherwise <c>null</c>.</returns>
        public async Task<string?> SendMessageToGenesysAsync(IActivity activity, string mcsConversationId, CancellationToken cancellationToken, bool prefetchConversationId = false)
        {
            var authToken = await _tokenProvider.GetTokenAsync(cancellationToken);
            await StoreUserChannelReferenceAsync(activity, mcsConversationId, cancellationToken);
            return await SendMessageAsync(activity, mcsConversationId, authToken, prefetchConversationId, cancellationToken);
        }

        /// <summary>
        /// Deletes the stored <see cref="ConversationReference"/> for the given MCS conversation.
        /// </summary>
        public async Task DeleteUserChannelReferenceAsync(string mcsConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mcsConversationId))
            {
                return;
            }

            await _storage.DeleteAsync([mcsConversationId], cancellationToken);
        }

        /// <summary>
        /// Disconnects the Genesys conversation so the live agent is notified that the user has left.
        /// Retrieves the conversation to find the external participant, then sets their state to disconnected
        /// via <c>PATCH /api/v2/conversations/messages/{conversationId}/participants/{participantId}</c>.
        /// </summary>
        /// <param name="genesysConversationId">The Genesys conversation ID to disconnect.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task DisconnectConversationAsync(string genesysConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(genesysConversationId))
            {
                return;
            }

            var authToken = await _tokenProvider.GetTokenAsync(cancellationToken);
            using var client = _httpClientFactory.CreateClient();

            // Step 1: Get conversation details to find the external participant ID
            var getUrl = $"{_setting.ApiUrl}/api/v2/conversations/messages/{genesysConversationId}";
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, getUrl);
            getRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authToken);

            var getResponse = await client.SendAsync(getRequest, cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                var errorBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to retrieve Genesys conversation {GenesysConversationId} for disconnect. Status: {StatusCode}. Response: {Response}",
                    genesysConversationId, getResponse.StatusCode, errorBody);
                return;
            }

            var content = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            string? externalParticipantId = null;

            using (var doc = JsonDocument.Parse(content))
            {
                if (doc.RootElement.TryGetProperty("participants", out var participants))
                {
                    foreach (var participant in participants.EnumerateArray())
                    {
                        // The external participant represents the customer (our side) in the Open Messaging conversation
                        if (participant.TryGetProperty("purpose", out var purpose)
                            && string.Equals(purpose.GetString(), "customer", StringComparison.OrdinalIgnoreCase)
                            && participant.TryGetProperty("id", out var idElement))
                        {
                            externalParticipantId = idElement.GetString();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(externalParticipantId))
            {
                _logger.LogWarning("Could not find customer participant in Genesys conversation {GenesysConversationId}. Cannot disconnect.", genesysConversationId);
                return;
            }

            // Step 2: Disconnect the external participant
            var patchUrl = $"{_setting.ApiUrl}/api/v2/conversations/messages/{genesysConversationId}/participants/{externalParticipantId}";
            using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, patchUrl);
            patchRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authToken);
            patchRequest.Content = new StringContent("{\"state\":\"disconnected\"}", Encoding.UTF8, ApplicationJsonMediaType);

            var patchResponse = await client.SendAsync(patchRequest, cancellationToken);

            if (patchResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Disconnected customer participant from Genesys conversation {GenesysConversationId}.", genesysConversationId);
            }
            else
            {
                var errorBody = await patchResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to disconnect participant from Genesys conversation {GenesysConversationId}. Status: {StatusCode}. Response: {Response}",
                    genesysConversationId, patchResponse.StatusCode, errorBody);
            }
        }

        private async Task<string?> SendMessageAsync(IActivity activity, string mcsConversationId, string authToken, bool prefetchConversationId, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            using var request = CreateMessageRequest(activity, mcsConversationId, authToken, prefetchConversationId);

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send message to Genesys for conversation {ConversationId}. Status: {StatusCode}. Response: {Response}",
                    mcsConversationId, response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode(); // throw so callers know it failed
            }

            if (!prefetchConversationId)
            {
                return null;
            }

            // Parse the response to extract the Genesys conversation ID
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("conversationId", out var idElement))
                {
                    var conversationId = idElement.GetString();
                    _logger.LogInformation("Genesys conversation ID {GenesysConversationId} obtained for MCS conversation {ConversationId}.", conversationId, mcsConversationId);
                    return conversationId;
                }
            }
            catch (JsonException)
            {
                _logger.LogWarning("Could not parse Genesys conversationId from response for conversation {ConversationId}.", mcsConversationId);
            }

            return null;
        }

        private async Task StoreUserChannelReferenceAsync(IActivity activity, string mcsConversationId, CancellationToken cancellationToken)
        {
            var userChannelReference = activity.GetConversationReference();

            await _storage.WriteAsync(
                new Dictionary<string, object>
                {
                    { mcsConversationId, userChannelReference }
                },
                cancellationToken
            );
        }

        private HttpRequestMessage CreateMessageRequest(IActivity activity, string mcsConversationId, string authToken, bool prefetchConversationId)
        {
            var url = BuildMessageApiUrl(prefetchConversationId);
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authToken);
            request.Content = CreateMessageContent(activity, mcsConversationId);

            return request;
        }

        private string BuildMessageApiUrl(bool prefetchConversationId)
        {
            var url = $"{_setting.ApiUrl}/api/v2/conversations/messages/{_setting.IntegrationId}/inbound/open/message";
            if (prefetchConversationId)
            {
                url += "?prefetchConversationId=true";
            }
            return url;
        }

        private StringContent CreateMessageContent(IActivity activity, string mcsConversationId)
        {
            var payload = BuildMessagePayload(activity, mcsConversationId);
            var json = JsonSerializer.Serialize(payload);
            _logger.LogDebug("Genesys outbound payload for conversation {ConversationId}: {Payload}", mcsConversationId, json);
            return new StringContent(json, Encoding.UTF8, ApplicationJsonMediaType);
        }

        private object BuildMessagePayload(IActivity activity, string mcsConversationId)
        {
            return new
            {
                channel = BuildChannelInfo(activity, mcsConversationId),
                text = activity.Text ?? string.Empty
            };
        }

        private static object BuildChannelInfo(IActivity activity, string mcsConversationId)
        {
            return new
            {
                messageId = activity.Id,
                from = new
                {
                    nickname = activity.From.Name,
                    id = mcsConversationId,
                    idType = "Opaque",
                },
                time = DateTime.UtcNow.ToString("o")
            };
        }
    }
}

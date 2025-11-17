// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
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
    public class GenesysService(IGenesysConnectionSettings setting, IHttpClientFactory httpClientFactory, IStorage storage)
    {
        private readonly IGenesysConnectionSettings _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        private const string GrantType = "grant_type=client_credentials";
        private const string BearerScheme = "bearer";
        private const string BasicScheme = "Basic";
        private const string ApplicationJsonMediaType = "application/json";
        private const string FormUrlEncodedMediaType = "application/x-www-form-urlencoded";
        private const string AccessTokenProperty = "access_token";
        private const string ExpiresInProperty = "expires_in";

        // Token caching fields
        private string? _cachedToken;
        private DateTime _tokenExpiryTime;
        private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

        public async Task SendMessageToGenesysAsync(IActivity activity, string mcsConversationId, CancellationToken cancellationToken)
        {
            var authToken = await AuthenticateAsync(cancellationToken);
            await StoreConversationReferenceAsync(activity, mcsConversationId, cancellationToken);
            await SendMessageAsync(activity, mcsConversationId, authToken, cancellationToken);
        }

        public async Task RetrieveMessageFromGenesysAsync(HttpRequest request, IChannelAdapter channelAdapter, CancellationToken cancellationToken)
        {
            var payload = await JsonSerializer.DeserializeAsync<GenesysOutboundPayload>(request.Body, cancellationToken: cancellationToken);

            if (payload == null || payload.Channel == null || payload.Channel.To == null || payload.Channel.To.Id == null)
            {
                return;
            }

            var c2ConversationId = payload.Channel.To.Id;
            var state = await _storage.ReadAsync([c2ConversationId], cancellationToken);

            if (!state.TryGetValue(c2ConversationId, out var referenceObj) || referenceObj is not ConversationReference conversationReference)
            {
                return;
            }

            if (string.IsNullOrEmpty(payload.Text))
            {
                // If payload is coming as empty send a typing text.
                await channelAdapter.ContinueConversationAsync(
                agentId: conversationReference.Agent.Id,
                reference: conversationReference,
                callback: async (turnContext, ct) =>
                {
                    await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, ct);
                }, cancellationToken: cancellationToken);
                return;
            }


            await channelAdapter.ContinueConversationAsync(
                agentId: conversationReference.Agent.Id, // Replace with your app ID or retrieve from config
                reference: conversationReference,
                callback: async (turnContext, ct) =>
                {
                    // Create a more descriptive reply activity with proper formatting
                    var agentPrefix = "[Live Agent]";
                    var fullMessage = $"{agentPrefix} - {payload.Text}";
                    var replyActivity = MessageFactory.Text(fullMessage);

                    if (payload.ContentData != null && payload.ContentData.Count > 0)
                    {
                        // Handle attachments if any
                        var attachments = new List<Attachment>();
                        foreach (var content in payload.ContentData)
                        {
                            if (content.Attachment != null && !string.IsNullOrEmpty(content.Attachment.Mime) && !string.IsNullOrEmpty(content.Attachment.Url))
                            {
                                attachments.Add(new Attachment
                                {
                                    ContentType = content.Attachment.Mime,
                                    ContentUrl = content.Attachment.Url,
                                    Name = content.Attachment.FileName
                                });
                            }
                        }
                        replyActivity.Attachments = attachments;
                    }
                    await turnContext.SendActivityAsync(replyActivity, cancellationToken: ct);
                },
                cancellationToken: cancellationToken
            );
        }

        private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
        {
            await _tokenSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check if cached token is still valid
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTime)
                {
                    return _cachedToken;
                }

                // Request new token
                using var client = _httpClientFactory.CreateClient();
                using var request = CreateAuthenticationRequest();

                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var (token, expiresIn) = await ExtractTokenDataAsync(response, cancellationToken);

                // Cache the token with expiry time (subtract 60 seconds as buffer)
                _cachedToken = token;
                _tokenExpiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                return token;
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        private HttpRequestMessage CreateAuthenticationRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _setting.OauthUrl);
            var credentials = GetEncodedCredentials();

            request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, credentials);
            request.Content = new StringContent(GrantType, Encoding.UTF8, FormUrlEncodedMediaType);

            return request;
        }

        private string GetEncodedCredentials()
        {
            var credentialString = $"{_setting.ClientId}:{_setting.ClientSecret}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialString));
        }

        private static async Task<(string token, int expiresIn)> ExtractTokenDataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = JsonDocument.Parse(content);

            if (!document.RootElement.TryGetProperty(AccessTokenProperty, out var tokenElement))
            {
                throw new InvalidOperationException("Authentication token is missing in the response.");
            }

            var token = tokenElement.GetString();
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Authentication token is empty.");
            }

            // Extract expires_in if available, default to 3600 seconds (1 hour) if not present
            var expiresIn = 3600;
            if (document.RootElement.TryGetProperty(ExpiresInProperty, out var expiresElement))
            {
                expiresIn = expiresElement.GetInt32();
            }

            return (token, expiresIn);
        }

        private async Task SendMessageAsync(IActivity activity, string mcsConversationId, string authToken, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            using var request = CreateMessageRequest(activity, mcsConversationId, authToken);

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task StoreConversationReferenceAsync(IActivity activity, string mcsConversationId, CancellationToken cancellationToken)
        {
            var conversationReference = activity.GetConversationReference();

            await _storage.WriteAsync(
                new Dictionary<string, object>
                {
                { mcsConversationId, conversationReference }
                },
                cancellationToken
            );
        }

        private HttpRequestMessage CreateMessageRequest(IActivity activity, string mcsConversationId, string authToken)
        {
            var url = BuildMessageApiUrl();
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authToken);
            request.Content = CreateMessageContent(activity, mcsConversationId);

            return request;
        }

        private string BuildMessageApiUrl()
        {
            return $"{_setting.ApiUrl}/api/v2/conversations/messages/{_setting.IntegrationId}/inbound/open/message";
        }

        private StringContent CreateMessageContent(IActivity activity, string mcsConversationId)
        {
            var payload = BuildMessagePayload(activity, mcsConversationId);
            var json = JsonSerializer.Serialize(payload);
            return new StringContent(json, Encoding.UTF8, ApplicationJsonMediaType);
        }

        private object BuildMessagePayload(IActivity activity, string mcsConversationId)
        {
            return new
            {
                channel = BuildChannelInfo(activity, mcsConversationId),
                text = activity.Text,
                content = BuildAttachmentContent(activity.Attachments)
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

        private static List<object>? BuildAttachmentContent(IList<Attachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return null;
            }

            var content = new List<object>();
            foreach (var attachment in attachments)
            {
                if (attachment.ContentType != "text/html")
                {
                    content.Add(CreateAttachmentObject(attachment));
                }
            }
            return content;
        }

        private static object CreateAttachmentObject(Attachment attachment)
        {
            return new
            {
                attachment = new
                {
                    mediaType = DetermineMediaType(attachment.ContentType),
                    url = attachment.ContentUrl,
                    id = Guid.NewGuid(),
                    mime = attachment.ContentType,
                    fileName = attachment.Name,
                }
            };
        }

        private static string DetermineMediaType(string contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/png" or "image/jpeg" or "image/jpg" or "image/gif" => "Image",
                "video/mp4" or "video/mpeg" => "Video",
                "audio/mpeg" or "audio/wav" => "Audio",
                "application/pdf" => "File",
                _ => "File"
            };
        }
    }
}
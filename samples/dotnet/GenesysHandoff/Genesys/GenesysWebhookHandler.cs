// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Represents the outcome of processing an inbound Genesys webhook request.
    /// </summary>
    public enum WebhookResult
    {
        /// <summary>Webhook signature validation failed — the request is not authentic.</summary>
        Unauthorized,

        /// <summary>Request was authentic but no message was forwarded (e.g., missing payload, unknown conversation, or malformed body).</summary>
        Accepted,

        /// <summary>Request was authentic and a message (or typing indicator) was forwarded to the user.</summary>
        MessageSent
    }

    /// <summary>
    /// Handles inbound webhook requests from Genesys Cloud, validates signatures,
    /// and forwards agent messages to the Teams user via proactive messaging.
    /// </summary>
    public class GenesysWebhookHandler(IGenesysConnectionSettings setting, IStorage storage, ILogger<GenesysWebhookHandler> logger)
    {
        private readonly IGenesysConnectionSettings _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        private readonly ILogger<GenesysWebhookHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Processes an inbound webhook request from Genesys.
        /// Validates the signature, deserializes the payload, and proactively
        /// sends the agent's message (or a typing indicator) to the user.
        /// </summary>
        /// <returns>A <see cref="WebhookResult"/> indicating whether the request was unauthorized, accepted without forwarding, or forwarded to the user.</returns>
        public async Task<WebhookResult> HandleAsync(HttpRequest request, IChannelAdapter channelAdapter, CancellationToken cancellationToken)
        {
            // Read the request body and validate signature
            string requestBody;
            request.EnableBuffering();
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync(cancellationToken);
                request.Body.Position = 0;
            }

            if (!ValidateWebhookSignature(request, requestBody))
            {
                _logger.LogWarning("Webhook signature validation failed for incoming request.");
                return WebhookResult.Unauthorized;
            }

            GenesysOutboundPayload? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<GenesysOutboundPayload>(requestBody);

                if (payload?.Channel?.To?.Id == null)
                {
                    return WebhookResult.Accepted;
                }

                var c2ConversationId = payload.Channel.To.Id;
                var state = await _storage.ReadAsync([c2ConversationId], cancellationToken);

                if (!state.TryGetValue(c2ConversationId, out var referenceObj) || referenceObj is not ConversationReference userChannelReference)
                {
                    return WebhookResult.Accepted;
                }

                // Use a separate cancellation token for proactive sends so they complete
                // even if the incoming Genesys webhook request is dropped.
                using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var sendToken = sendCts.Token;

                if (string.IsNullOrEmpty(payload.Text))
                {
                    // Empty payload — send a typing indicator.
                    await SendProactiveActivityAsync(channelAdapter, userChannelReference, new Activity { Type = ActivityTypes.Typing }, sendToken);
                    return WebhookResult.MessageSent;
                }

                // Build and send the agent's message
                var replyActivity = BuildAgentReply(payload);
                await SendProactiveActivityAsync(channelAdapter, userChannelReference, replyActivity, sendToken);
                return WebhookResult.MessageSent;
            }
            catch (JsonException)
            {
                return WebhookResult.Accepted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Genesys webhook for conversation {ConversationId}.", payload?.Channel?.To?.Id);
                throw;
            }
        }

        private static async Task SendProactiveActivityAsync(IChannelAdapter channelAdapter, ConversationReference userChannelReference, IActivity activity, CancellationToken cancellationToken)
        {
            var continuationActivity = userChannelReference.GetContinuationActivity();
            var claimsIdentity = AgentClaims.CreateIdentity(userChannelReference.Agent.Id);

            await channelAdapter.ProcessProactiveAsync(
                claimsIdentity: claimsIdentity,
                continuationActivity: continuationActivity,
                audience: string.Empty,
                callback: async (turnContext, ct) =>
                {
                    await turnContext.SendActivityAsync(activity, cancellationToken: ct);
                },
                cancellationToken: cancellationToken);
        }

        private const string EndLiveChatAction = "End chat with agent";

        private static IActivity BuildAgentReply(GenesysOutboundPayload payload)
        {
            var replyActivity = MessageFactory.Text($"[Live Agent] - {payload.Text}");
            replyActivity.SuggestedActions = new SuggestedActions
            {
                Actions = new List<CardAction>
                {
                    new() { Title = EndLiveChatAction, Type = ActionTypes.ImBack, Value = EndLiveChatAction }
                }
            };

            if (payload.ContentData != null && payload.ContentData.Count > 0)
            {
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

            return replyActivity;
        }

        /// <summary>
        /// Validates the webhook signature from Genesys using HMAC-SHA256.
        /// </summary>
        private bool ValidateWebhookSignature(HttpRequest request, string requestBody)
        {
            const string SignatureHeaderName = "X-Hub-Signature-256";
            const string SignaturePrefix = "sha256=";

            if (!request.Headers.TryGetValue(SignatureHeaderName, out var signatureHeader))
            {
                return false;
            }

            var signature = signatureHeader.ToString();
            if (string.IsNullOrEmpty(signature) || !signature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var secretBytes = Encoding.UTF8.GetBytes(_setting.WebhookSignatureSecret!);
            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);

            using var hmac = new HMACSHA256(secretBytes);
            var hashBytes = hmac.ComputeHash(bodyBytes);
            var expectedHash = Convert.ToBase64String(hashBytes);
            var expectedSignature = $"{SignaturePrefix}{expectedHash}";

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(signature));
        }
    }
}

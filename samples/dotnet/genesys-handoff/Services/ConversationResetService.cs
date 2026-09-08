// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GenesysHandoff.Genesys;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Service for resetting conversations externally by conversation ID.
    /// This service cleans up local state and channel references.
    /// Note: Graceful Genesys disconnection requires access to turn state and is handled by the agent.
    /// </summary>
    public class ConversationResetService
    {
        private const string ResetRequestedStoragePrefix = "conversation_reset_requested_";
        private const string DefaultResetConversationMessage = "The conversation has been reset by the system. Please start a new conversation if you need further assistance.";

        private readonly IStorage _storage;
        private readonly GenesysMessageSender _messageSender;
        private readonly IActivityReplyMappingStore _activityReplyMappingStore;
        private readonly ConversationStateManager _stateManager;
        private readonly IChannelAdapter _channelAdapter;
        private readonly IGenesysConnectionSettings _settings;
        private readonly ILogger<ConversationResetService> _logger;

        public ConversationResetService(
            IStorage storage,
            GenesysMessageSender messageSender,
            IActivityReplyMappingStore activityReplyMappingStore,
            ConversationStateManager stateManager,
            IChannelAdapter channelAdapter,
            IGenesysConnectionSettings settings,
            ILogger<ConversationResetService> logger)
        {
            _storage = storage;
            _messageSender = messageSender;
            _activityReplyMappingStore = activityReplyMappingStore;
            _stateManager = stateManager;
            _channelAdapter = channelAdapter;
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Resets a conversation by cleaning up associated resources and clearing conversation state.
        /// Only resets if the conversation has not been escalated to a human agent.
        /// If <paramref name="messageText"/> is provided, it is sent to the Teams conversation before the reference is deleted.
        /// </summary>
        /// <param name="mcsConversationId">The MCS conversation ID to reset.</param>
        /// <param name="messageText">Optional message to send to the Teams conversation before resetting.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True if reset successfully; false if the conversation is escalated.</returns>
        public async Task<bool> ResetConversationAsync(string mcsConversationId, string? messageText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mcsConversationId))
            {
                throw new ArgumentException("Conversation ID cannot be null or empty.", nameof(mcsConversationId));
            }

            // Only reset non-escalated conversations via this route
            var isEscalated = await _stateManager.IsEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);

            if (isEscalated)
            {
                _logger.LogWarning("Cannot reset conversation {ConversationId} because it has been escalated to a human agent.", mcsConversationId);
                return false;
            }

            _logger.LogInformation("Resetting conversation {ConversationId}.", mcsConversationId);

            try
            {
                await MarkConversationResetRequestedAsync(mcsConversationId, cancellationToken);

                messageText ??= _settings.ResetConversationMessage ?? DefaultResetConversationMessage;
                // Send a message to the Teams conversation before deleting the reference
                if (!string.IsNullOrEmpty(messageText))
                {
                    var activity = MessageFactory.Text(messageText);
                    var messageSent = await _messageSender.SendProactiveMessageAsync(mcsConversationId, activity, _channelAdapter, cancellationToken);
                    if (!messageSent)
                    {
                        _logger.LogWarning("Failed to send message to conversation {ConversationId}, but proceeding with reset.", mcsConversationId);
                    }
                }

                // Delete the user channel reference (stored in IStorage with key = mcsConversationId)
                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                await _activityReplyMappingStore.DeleteConversationMappingsAsync(mcsConversationId, cancellationToken);

                _logger.LogInformation("Successfully reset conversation {ConversationId}.", mcsConversationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during conversation reset for {ConversationId}.", mcsConversationId);
                throw;
            }
        }

        /// <summary>
        /// Checks whether a reset was requested for the given conversation and clears the persisted marker.
        /// </summary>
        /// <param name="mcsConversationId">The MCS conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><c>true</c> if a reset request was found and cleared; otherwise, <c>false</c>.</returns>
        public async Task<bool> CheckAndClearResetRequestedAsync(string mcsConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mcsConversationId))
            {
                return false;
            }

            var key = GetResetRequestedStorageKey(mcsConversationId);
            var state = await _storage.ReadAsync([key], cancellationToken);

            if (!state.TryGetValue(key, out _))
            {
                return false;
            }

            await _storage.DeleteAsync([key], cancellationToken);
            return true;
        }

        private async Task MarkConversationResetRequestedAsync(string mcsConversationId, CancellationToken cancellationToken)
        {
            var key = GetResetRequestedStorageKey(mcsConversationId);

            await _storage.WriteAsync(
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { key, new { resetRequested = true } }
                },
                cancellationToken);
        }

        private static string GetResetRequestedStorageKey(string mcsConversationId) => $"{ResetRequestedStoragePrefix}{mcsConversationId}";
    }
}

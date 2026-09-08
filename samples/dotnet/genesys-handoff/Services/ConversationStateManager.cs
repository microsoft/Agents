// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Manages conversation state properties for Genesys handoff scenarios.
    /// </summary>
    public class ConversationStateManager
    {
        private const string MCSConversationPropertyName = "MCSConversationId";
        private const string IsEscalatedPropertyName = "IsEscalated";
        private const string EscalatedStoragePrefix = "conversation_escalated_";
        private const string LastCopilotStudioReferencePropertyName = "LastCopilotStudioReference";
        private const string GenesysConversationIdPropertyName = "GenesysConversationId";
        private readonly ILogger<ConversationStateManager> _logger;

        public ConversationStateManager(ILogger<ConversationStateManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the Copilot Studio conversation ID from the turn state.
        /// Returns null if the conversation ID has not been set.
        /// </summary>
        /// <param name="turnState">The turn state containing conversation properties.</param>
        /// <returns>The conversation ID if it exists; otherwise, null.</returns>
        public string? GetConversationId(ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            return turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
        }

        /// <summary>
        /// Sets the Copilot Studio conversation ID in the turn state.
        /// </summary>
        /// <param name="turnState">The turn state to update.</param>
        /// <param name="conversationId">The conversation ID to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when turnState is null.</exception>
        /// <exception cref="ArgumentException">Thrown when conversationId is null or empty.</exception>
        public void SetConversationId(ITurnState turnState, string conversationId)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            ArgumentException.ThrowIfNullOrEmpty(conversationId);
            turnState.Conversation.SetValue(MCSConversationPropertyName, conversationId);
        }

        /// <summary>
        /// Gets whether the conversation has been escalated to a human agent.
        /// Returns false if the escalation flag has not been set (default behavior for new conversations).
        /// </summary>
        /// <param name="turnState">The turn state containing conversation properties.</param>
        /// <returns>True if the conversation has been escalated; otherwise, false.</returns>
        public bool IsEscalated(ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            // GetValue<bool> returns false (default) if the property doesn't exist,
            // which is the desired behavior for new conversations
            return turnState.Conversation.GetValue<bool>(IsEscalatedPropertyName);
        }

        /// <summary>
        /// Marks the conversation as escalated to a human agent.
        /// </summary>
        /// <param name="turnState">The turn state to update.</param>
        /// <param name="isEscalated">True to mark as escalated; false otherwise.</param>
        public void SetEscalated(ITurnState turnState, bool isEscalated)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            turnState.Conversation.SetValue(IsEscalatedPropertyName, isEscalated);
        }

        /// <summary>
        /// Gets the last conversation reference from Copilot Studio, or <c>null</c> if none has been stored.
        /// </summary>
        /// <param name="turnState">The turn state containing conversation properties.</param>
        /// <returns>The last Copilot Studio conversation reference if it exists; otherwise, <c>null</c>.</returns>
        public ConversationReference? GetLastCopilotStudioReference(ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            return turnState.Conversation.GetValue<ConversationReference>(LastCopilotStudioReferencePropertyName);
        }

        /// <summary>
        /// Stores a conversation reference from Copilot Studio in conversation state for stitching.
        /// </summary>
        /// <param name="turnState">The turn state to update.</param>
        /// <param name="conversationReference">The conversation reference to store.</param>
        public void SetLastCopilotStudioReference(ITurnState turnState, ConversationReference conversationReference)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            ArgumentNullException.ThrowIfNull(conversationReference);
            turnState.Conversation.SetValue(LastCopilotStudioReferencePropertyName, conversationReference);
        }

        /// <summary>
        /// Clears all conversation state properties managed by this class.
        /// </summary>
        /// <param name="turnState">The turn state to clear.</param>
        public string? GetGenesysConversationId(ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            return turnState.Conversation.GetValue<string>(GenesysConversationIdPropertyName);
        }

        public void SetGenesysConversationId(ITurnState turnState, string conversationId)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            ArgumentException.ThrowIfNullOrEmpty(conversationId);
            turnState.Conversation.SetValue(GenesysConversationIdPropertyName, conversationId);
        }

        public void ClearConversationState(ITurnState turnState)
        {
            ArgumentNullException.ThrowIfNull(turnState);
            turnState.Conversation.DeleteValue(MCSConversationPropertyName);
            turnState.Conversation.DeleteValue(IsEscalatedPropertyName);
            turnState.Conversation.DeleteValue(LastCopilotStudioReferencePropertyName);
            turnState.Conversation.DeleteValue(GenesysConversationIdPropertyName);
        }

        /// <summary>
        /// Persists an escalation marker for the conversation, keyed by the MCS conversation ID, so that
        /// the escalation state can be read outside of a turn (e.g. by the external reset API).
        /// </summary>
        /// <param name="storage">The storage instance to write to.</param>
        /// <param name="conversationId">The MCS conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async System.Threading.Tasks.Task SetEscalatedInStorageAsync(IStorage storage, string conversationId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(storage);
            ArgumentException.ThrowIfNullOrEmpty(conversationId);

            var key = GetEscalatedStorageKey(conversationId);
            await storage.WriteAsync(
                new Dictionary<string, object> { { key, new { escalated = true } } },
                cancellationToken);
        }

        /// <summary>
        /// Removes the persisted escalation marker for the conversation. Safe to call when no marker exists.
        /// </summary>
        /// <param name="storage">The storage instance to delete from.</param>
        /// <param name="conversationId">The MCS conversation ID.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async System.Threading.Tasks.Task ClearEscalatedInStorageAsync(IStorage storage, string conversationId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(storage);
            if (string.IsNullOrEmpty(conversationId))
            {
                return;
            }

            await storage.DeleteAsync([GetEscalatedStorageKey(conversationId)], cancellationToken);
        }

        /// <summary>
        /// Checks if a conversation is escalated by reading the persisted escalation marker from storage.
        /// </summary>
        /// <param name="storage">The storage instance to read from.</param>
        /// <param name="conversationId">The MCS conversation ID to check.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True if the conversation has been escalated; otherwise, false.</returns>
        public async System.Threading.Tasks.Task<bool> IsEscalatedInStorageAsync(IStorage storage, string conversationId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(storage);
            ArgumentException.ThrowIfNullOrEmpty(conversationId);

            try
            {
                var key = GetEscalatedStorageKey(conversationId);
                var result = await storage.ReadAsync([key], cancellationToken);
                return result != null && result.ContainsKey(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read escalation state from storage for conversation {ConversationId}. Defaulting to not escalated.", conversationId);
                // If unable to read or parse state, assume not escalated (default)
                return false;
            }
        }

        private static string GetEscalatedStorageKey(string conversationId) => $"{EscalatedStoragePrefix}{conversationId}";
    }
}

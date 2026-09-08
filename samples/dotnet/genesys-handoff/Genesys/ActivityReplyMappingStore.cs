// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Stores per-conversation mappings between Relay Bot activity IDs and MCS activity IDs.
    /// This enables setting ReplyToId when forwarding user replies from Teams to MCS.
    /// </summary>
    public class ActivityReplyMappingStore : IActivityReplyMappingStore
    {
        private const string ActivityReplyMapPrefix = "activity_reply_map_";

        private readonly IStorage _storage;
        private readonly ILogger<ActivityReplyMappingStore> _logger;

        public ActivityReplyMappingStore(IStorage storage, ILogger<ActivityReplyMappingStore> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        /// <summary>
        /// Adds or updates the mapping from a Relay Bot activity ID to an MCS activity ID.
        /// </summary>
        public async Task UpsertAsync(string mcsConversationId, string relayActivityId, string mcsActivityId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mcsConversationId)
                || string.IsNullOrWhiteSpace(relayActivityId)
                || string.IsNullOrWhiteSpace(mcsActivityId))
            {
                return;
            }

            var key = GetStorageKey(mcsConversationId);
            var map = await ReadConversationMapAsync(key, cancellationToken);

            map[relayActivityId] = mcsActivityId;

            await _storage.WriteAsync(
                new Dictionary<string, object>
                {
                    { key, map }
                },
                cancellationToken);
        }

        /// <summary>
        /// Looks up the MCS activity ID for the given Relay Bot activity ID within a conversation.
        /// </summary>
        public async Task<string?> GetMcsActivityIdAsync(string mcsConversationId, string relayActivityId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mcsConversationId) || string.IsNullOrWhiteSpace(relayActivityId))
            {
                return null;
            }

            var key = GetStorageKey(mcsConversationId);
            var map = await ReadConversationMapAsync(key, cancellationToken);

            return map.TryGetValue(relayActivityId, out var mcsActivityId)
                ? mcsActivityId
                : null;
        }

        /// <summary>
        /// Deletes all activity reply mappings for a conversation.
        /// </summary>
        public async Task DeleteConversationMappingsAsync(string mcsConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mcsConversationId))
            {
                return;
            }

            await _storage.DeleteAsync([GetStorageKey(mcsConversationId)], cancellationToken);
        }

        /// <summary>
        /// Reads the per-conversation reply mapping dictionary from storage.
        /// Returns an empty dictionary (never <c>null</c>) when no mapping exists
        /// or when the stored payload cannot be parsed into the expected shape.
        /// </summary>
        private async Task<Dictionary<string, string>> ReadConversationMapAsync(string key, CancellationToken cancellationToken)
        {
            var state = await _storage.ReadAsync([key], cancellationToken);
            if (!state.TryGetValue(key, out var rawValue))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (rawValue is Dictionary<string, string> typedMap)
            {
                return new Dictionary<string, string>(typedMap, StringComparer.Ordinal);
            }

            if (rawValue is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in json.EnumerateObject())
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        map[property.Name] = value;
                    }
                }

                return map;
            }

            _logger.LogWarning("Unexpected activity mapping payload type for key {StorageKey}.", key);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private static string GetStorageKey(string mcsConversationId) => $"{ActivityReplyMapPrefix}{mcsConversationId}";
    }
}

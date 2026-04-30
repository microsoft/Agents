// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Maintains an in-memory mapping of Genesys conversation IDs to MCS conversation IDs,
    /// backed by <see cref="IStorage"/> for persistence across restarts.
    /// </summary>
    public class ConversationMappingStore
    {
        private const string ConversationRegistryKey = "genesys_conversation_registry";

        private readonly IStorage _storage;
        private readonly ILogger<ConversationMappingStore> _logger;
        private readonly ConcurrentDictionary<string, string> _conversationMap = new();

        public ConversationMappingStore(IStorage storage, ILogger<ConversationMappingStore> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        /// <summary>
        /// Adds a mapping and persists the registry.
        /// </summary>
        public async Task AddAsync(string genesysConversationId, string mcsConversationId, CancellationToken cancellationToken)
        {
            _conversationMap[genesysConversationId] = mcsConversationId;
            await PersistAsync(cancellationToken);
        }

        /// <summary>
        /// Removes a mapping by Genesys conversation ID, persists, and returns the MCS conversation ID if found.
        /// </summary>
        public async Task<string?> RemoveAsync(string genesysConversationId, CancellationToken cancellationToken)
        {
            if (!_conversationMap.TryRemove(genesysConversationId, out var mcsConversationId))
            {
                return null;
            }

            await PersistAsync(cancellationToken);
            return mcsConversationId;
        }

        /// <summary>
        /// Returns all Genesys conversation IDs currently tracked.
        /// </summary>
        public ICollection<string> GetAllGenesysConversationIds() => _conversationMap.Keys;

        /// <summary>
        /// Returns <c>true</c> if there are no tracked conversations.
        /// </summary>
        public bool IsEmpty => _conversationMap.IsEmpty;

        /// <summary>
        /// Loads the conversation map from persisted storage.
        /// </summary>
        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            var state = await _storage.ReadAsync([ConversationRegistryKey], cancellationToken);
            if (!state.TryGetValue(ConversationRegistryKey, out var registryObj))
            {
                return;
            }

            if (registryObj is Dictionary<string, string> registry)
            {
                foreach (var kvp in registry)
                {
                    _conversationMap.TryAdd(kvp.Key, kvp.Value);
                }
            }
            else if (registryObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in jsonElement.EnumerateObject())
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        _conversationMap.TryAdd(property.Name, value);
                    }
                }
            }

            _logger.LogInformation("Loaded {Count} conversation mappings from storage.", _conversationMap.Count);
        }

        private async Task PersistAsync(CancellationToken cancellationToken)
        {
            var registry = new Dictionary<string, string>(_conversationMap);
            await _storage.WriteAsync(
                new Dictionary<string, object> { { ConversationRegistryKey, registry } },
                cancellationToken);
        }
    }
}

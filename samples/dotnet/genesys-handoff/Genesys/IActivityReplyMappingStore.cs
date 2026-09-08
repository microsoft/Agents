// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff.Genesys
{
    /// <summary>
    /// Provides per-conversation mappings between Relay Bot activity IDs and MCS activity IDs.
    /// </summary>
    public interface IActivityReplyMappingStore
    {
        Task UpsertAsync(string mcsConversationId, string relayActivityId, string mcsActivityId, CancellationToken cancellationToken);

        Task<string?> GetMcsActivityIdAsync(string mcsConversationId, string relayActivityId, CancellationToken cancellationToken);

        Task DeleteConversationMappingsAsync(string mcsConversationId, CancellationToken cancellationToken);
    }
}

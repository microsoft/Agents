// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Blobs;

/// <summary>
/// Creates an <see cref="IStorage"/> instance from validated <see cref="StorageOptions"/>.
/// </summary>
public static class AgentStorageFactory
{
    /// <summary>
    /// Creates an <see cref="IStorage"/> backed by Azure Blob Storage.
    /// </summary>
    /// <param name="options">Validated storage options. Call <see cref="StorageOptions.Validate"/> before passing.</param>
    /// <param name="credential">
    /// Token credential used for managed-identity authentication when <see cref="StorageOptions.ContainerUri"/> is set.
    /// Ignored when <see cref="StorageOptions.ConnectionString"/> is set.
    /// </param>
    /// <returns>A configured <see cref="IStorage"/> instance.</returns>
    public static IStorage Create(StorageOptions options, TokenCredential credential)
    {
        if (options.ContainerUri is not null)
        {
            // Managed-identity path: BlobsStorage(Uri containerUri, TokenCredential credential, ...)
            return new BlobsStorage(options.ContainerUri, credential);
        }

        // Connection-string path: BlobsStorage(string connectionString, string containerName, ...)
        return new BlobsStorage(options.ConnectionString!, options.ContainerName);
    }
}

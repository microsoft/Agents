// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Web;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;

namespace ProductionReference;

/// <summary>
/// Store AgentState dictionaries with per-turn Blob versions. The released
/// AgentState does not carry IStoreItem ETags in its dictionary snapshots.
/// </summary>
public sealed class VersionedStorage(BlobContainerClient container) : IStorage
{
    private const string VersionKey = "__productionReferenceBlobVersion";

    public async Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var items = new Dictionary<string, object>();
        foreach (var key in keys)
        {
            try
            {
                var downloaded = await container.GetBlobClient(HttpUtility.UrlEncode(key))
                    .DownloadContentAsync(cancellationToken);
                var state = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    downloaded.Value.Content, ProtocolJsonSerializer.SerializationOptions)
                    ?? throw new InvalidDataException("Invalid stored state.");
                // Keep the version in this turn's dictionary, never a shared cache.
                state[VersionKey] = downloaded.Value.Details.ETag.ToString();
                items[key] = state;
            }
            catch (RequestFailedException error) when (error.Status == 404)
            {
                // First saves use a conditional create.
            }
        }
        return items;
    }

    public async Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default)
        where TStoreItem : class
    {
        var items = await ReadAsync(keys, cancellationToken);
        return items.Where(item => item.Value is TStoreItem)
            .ToDictionary(item => item.Key, item => (TStoreItem)item.Value);
    }

    public async Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        foreach (var (key, value) in changes)
        {
            var state = value as IDictionary<string, object>;
            var version = state != null && state.TryGetValue(VersionKey, out var token)
                ? token as string : null;
            var payload = state == null ? value : state.Where(item => item.Key != VersionKey)
                .ToDictionary(item => item.Key, item => item.Value);
            var conditions = string.IsNullOrEmpty(version)
                ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                : new BlobRequestConditions { IfMatch = new ETag(version) };
            var data = BinaryData.FromObjectAsJson(payload, ProtocolJsonSerializer.SerializationOptions);
            await container.GetBlobClient(HttpUtility.UrlEncode(key)).UploadAsync(data,
                new BlobUploadOptions
                {
                    Conditions = conditions,
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                }, cancellationToken);
        }
    }

    public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default)
        where TStoreItem : class
        => WriteAsync(changes.ToDictionary(item => item.Key, item => (object)item.Value), cancellationToken);

    public async Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            await container.GetBlobClient(HttpUtility.UrlEncode(key))
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }
}

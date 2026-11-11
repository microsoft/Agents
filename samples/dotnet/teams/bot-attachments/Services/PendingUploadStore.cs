// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System;

namespace Microsoft.Teams.Samples.BotAttachments.Services;

public sealed class PendingUploadStore
{
    private const int Capacity = 32;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, PendingUpload> _uploads = new();
    private readonly object _gate = new();

    public void Add(string fileId, byte[] content)
    {
        lock (_gate)
        {
            RemoveExpired();
            if (_uploads.Count >= Capacity)
            {
                throw new InvalidOperationException("Too many pending file uploads.");
            }
            _uploads[fileId] = new PendingUpload(content, DateTimeOffset.UtcNow);
        }
    }

    public bool TryTake(string fileId, out byte[] content)
    {
        if (!_uploads.TryRemove(fileId, out PendingUpload? pending) ||
            DateTimeOffset.UtcNow - pending.CreatedAt > Lifetime)
        {
            content = Array.Empty<byte>();
            return false;
        }
        content = pending.Content;
        return true;
    }

    public void Remove(string fileId)
    {
        _uploads.TryRemove(fileId, out _);
    }

    private void RemoveExpired()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach ((string key, PendingUpload value) in _uploads)
        {
            if (now - value.CreatedAt > Lifetime) _uploads.TryRemove(key, out _);
        }
    }

    private sealed record PendingUpload(byte[] Content, DateTimeOffset CreatedAt);
}

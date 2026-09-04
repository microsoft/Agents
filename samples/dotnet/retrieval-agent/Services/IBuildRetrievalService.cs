// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RetrievalAgent.Services;

public interface IBuildRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string question, Func<CancellationToken, Task<string>> getAccessTokenAsync, CancellationToken cancellationToken);
}

public enum RetrievalStatus
{
    Success,
    NotSignedIn,
    NoResults,
    ServiceUnavailable,
}

public sealed record RetrievalItem(string Title, string Extract, string SourceUrl);

public sealed record RetrievalResult(RetrievalStatus Status, IReadOnlyList<RetrievalItem> Items)
{
    public static RetrievalResult Failure(RetrievalStatus status) => new(status, Array.Empty<RetrievalItem>());

    public static RetrievalResult Success(IReadOnlyList<RetrievalItem> items) => new(RetrievalStatus.Success, items);
}

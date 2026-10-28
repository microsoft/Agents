// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RetrievalAgent.Services;

public interface IBuildGenieMessageRoute
{
    Task HandleAsync(
        string question,
        Func<CancellationToken, Task<string>> getAccessTokenAsync,
        Func<string, CancellationToken, Task> sendTextAsync,
        Func<IActivity, CancellationToken, Task> sendActivityAsync,
        CancellationToken cancellationToken);
}

public sealed class BuildGenieMessageRoute(IBuildRetrievalService retrievalService) : IBuildGenieMessageRoute
{
    public async Task HandleAsync(
        string question,
        Func<CancellationToken, Task<string>> getAccessTokenAsync,
        Func<string, CancellationToken, Task> sendTextAsync,
        Func<IActivity, CancellationToken, Task> sendActivityAsync,
        CancellationToken cancellationToken)
    {
        RetrievalResult result = await retrievalService.RetrieveAsync(question, getAccessTokenAsync, cancellationToken);

        if (result.Status != RetrievalStatus.Success)
        {
            await sendTextAsync(BuildGenieResponses.For(result.Status), cancellationToken);
            return;
        }

        await sendTextAsync(BuildGenieResponses.GroundedAnswer(result.Items), cancellationToken);
        await sendActivityAsync(BuildGenieSourceCard.Create(result.Items), cancellationToken);
    }
}

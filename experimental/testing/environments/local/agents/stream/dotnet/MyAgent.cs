// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace QuickStart;

public class MyAgent : AgentApplication
{

    static private readonly string fullText = "This is a streaming response.";
    static private readonly List<string> fullTextChunks = new List<string>(fullText.Split(' '));

    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnMessage("/stream", OnStreamAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnStreamAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        turnContext.StreamingResponse.QueueInformativeUpdateAsync("Starting stream...");

        await Task.Delay(1000); // Simulate delay before starting stream

        for (int i = 0; i < fullTextChunks.Count; i++)
        {
            turnContext.StreamingResponse.QueueTextChunk(fullTextChunks[i]);
            if (i < fullTextChunks.Count - 1)
            {
                await Task.Delay(1000); // Simulate delay between chunks
            }
        }
        await turnContext.StreamingResponse.EndStreamAsync();
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}
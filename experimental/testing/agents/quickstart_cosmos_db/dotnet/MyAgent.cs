// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
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
        OnMessage("/stream", SendStream);
        OnMessage("/error", TriggerErrorAsync);
        OnMessage("/language", async (turnContext, turnState, cancellationToken) =>
        {
            await turnContext.SendActivityAsync("DOTNET", cancellationToken: cancellationToken);
        });
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnConversationUpdate);
        OnActivity(ActivityTypes.Message, EchoMessage, rank: RouteRank.Last);
        OnTurnError(HandleTurnError);
    }

    private async Task SendStream(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Starting stream...");

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

    private async Task OnConversationUpdate(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync("Welcome", cancellationToken: cancellationToken);
    }

    private async Task TriggerErrorAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        throw new Exception("This is a triggered error for testing purposes.");
    }

    private async Task EchoMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    private async Task HandleTurnError(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync("The bot encountered an error or bug.", cancellationToken: cancellationToken);
    }
}
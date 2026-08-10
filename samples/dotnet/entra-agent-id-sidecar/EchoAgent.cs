// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EntraAgentIdSidecar;

public sealed class EchoAgent : AgentApplication
{
    private const string Usage =
        "Send `caller` to display inbound agentic token details. Other messages are echoed.";

    public EchoAgent(AgentApplicationOptions options)
        : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private static async Task WelcomeMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"Welcome! {Usage}"),
                    cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        string command = turnContext.Activity.Text?.Trim() ?? string.Empty;
        if (string.Equals(command, "caller", StringComparison.OrdinalIgnoreCase))
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Attachment(CallerCard.Create(turnContext)),
                cancellationToken);
            return;
        }

        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(Usage), cancellationToken);
            return;
        }

        await turnContext.SendActivityAsync(
            MessageFactory.Text($"Echo: {turnContext.Activity.Text ?? string.Empty}"),
            cancellationToken);
    }
}

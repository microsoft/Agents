// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Agent that runs the support triage flow, collecting issue details and persisting
/// them to conversation state via <see cref="SupportCaseStateAccessor"/>.
/// </summary>
public sealed class SupportTriageAgent : AgentApplication
{
    private readonly SupportCaseStateAccessor _accessor;
    private readonly SupportTriageDialog _dialog;
    private readonly ILogger<SupportTriageAgent> _logger;

    public SupportTriageAgent(
        AgentApplicationOptions options,
        SupportCaseStateAccessor accessor,
        SupportTriageDialog dialog,
        ILogger<SupportTriageAgent> logger)
        : base(options)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
        {
            _logger.LogError(exception, "Unhandled error in SupportTriageAgent");
            await turnContext.SendActivityAsync(
                "I'm sorry, something went wrong. Please try again.",
                cancellationToken: cancellationToken);
        });
    }

    private async Task WelcomeAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Hello! I can help you open a support case. What issue are you experiencing?"),
                    cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        SupportCaseState state = _accessor.Get(turnState);
        SupportTriageTurnResult result = _dialog.NextTurn(state, turnContext.Activity.Text, DateTimeOffset.UtcNow);
        _accessor.Set(turnState, result.State);
        await turnContext.SendActivityAsync(result.ResponseText, cancellationToken: cancellationToken);
    }
}
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cards;

public class CardsAgent : AgentApplication
{
    public CardsAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnMembersAddedAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private static async Task OnMembersAddedAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        string? agentId = turnContext.Activity.Recipient?.Id;
        if (turnContext.Activity.MembersAdded?.Any(member => member.Id != agentId) == true)
        {
            await CardMessages.SendIntroCardAsync(turnContext, cancellationToken);
        }
    }

    private static async Task OnMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        string input = turnContext.Activity.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            await turnContext.SendActivityAsync(
                "This sample is only for testing Cards using CardFactory methods. Please refer to other samples to test out more functionalities.",
                cancellationToken: cancellationToken);
            return;
        }

        string command = input.Length > 1 && input[0] is >= '1' and <= '7' && input[1] == '.'
            ? input[..1]
            : input;

        switch (command)
        {
            case "display card options":
                await CardMessages.SendIntroCardAsync(turnContext, cancellationToken);
                break;
            case "1":
                await CardMessages.SendAdaptiveCardAsync(turnContext, cancellationToken);
                break;
            case "2":
                await CardMessages.SendAnimationCardAsync(turnContext, cancellationToken);
                break;
            case "3":
                await CardMessages.SendAudioCardAsync(turnContext, cancellationToken);
                break;
            case "4":
                await CardMessages.SendHeroCardAsync(turnContext, cancellationToken);
                break;
            case "5":
                await CardMessages.SendReceiptCardAsync(turnContext, cancellationToken);
                break;
            case "6":
                await CardMessages.SendThumbnailCardAsync(turnContext, cancellationToken);
                break;
            case "7":
                await CardMessages.SendVideoCardAsync(turnContext, cancellationToken);
                break;
            default:
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Your input was not recognized, please try again."),
                    cancellationToken);
                await CardMessages.SendIntroCardAsync(turnContext, cancellationToken);
                break;
        }
    }
}

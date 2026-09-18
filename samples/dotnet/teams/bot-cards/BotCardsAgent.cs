// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using CardsHandler = Microsoft.Teams.Samples.BotCards.Handlers.Cards;

namespace Microsoft.Teams.Samples.BotCards;

[TeamsExtension]
public partial class BotCardsAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [TeamsMessageRoute(textRegex: "(?i)card actions", rank: 10)]
    public Task OnCardActionsAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
        => CardsHandler.SendAdaptiveCardActionsAsync(turnContext, cancellationToken);

    [TeamsMessageRoute(textRegex: "(?i)toggle visibility", rank: 20)]
    public Task OnToggleVisibilityAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
        => CardsHandler.SendToggleVisibilityCardAsync(turnContext, cancellationToken);

    [TeamsMessageRoute]
    public Task OnMessageAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
        => turnContext.SendActivityAsync(
            MessageFactory.Text(
                "Welcome to the Cards Bot! To interact with me, send one of the following commands: 'card actions' or 'toggle visibility'"),
            cancellationToken);

    [ActionExecuteRoute("submit_name")]
    public Task<AdaptiveCardInvokeResponse> OnSubmitNameAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        object data,
        CancellationToken cancellationToken)
    {
        JsonElement submittedData = JsonSerializer.SerializeToElement(data);
        string name = submittedData.TryGetProperty("name", out JsonElement nameProperty)
            ? nameProperty.ToString()
            : string.Empty;

        return Task.FromResult(
            AdaptiveCardInvokeResponseFactory.Message($"Data Submitted: {name}"));
    }
}

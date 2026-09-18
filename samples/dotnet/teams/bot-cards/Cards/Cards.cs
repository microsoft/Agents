// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;

namespace Microsoft.Teams.Samples.BotCards.Handlers;

public static class Cards
{
    public static Task SendAdaptiveCardActionsAsync(
        ITeamsTurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var adaptiveCard = new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Body =
            [
                new TextBlock("Adaptive Card Actions")
            ],
            Actions =
            [
                new OpenUrlAction("https://adaptivecards.io")
                {
                    Title = "Action Open URL"
                },
                new ShowCardAction
                {
                    Title = "Action Submit",
                    Card = new AdaptiveCard
                    {
                        Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                        Body =
                        [
                            new TextInput
                            {
                                Id = "name",
                                Label = "Please enter your name:",
                                IsRequired = true,
                                ErrorMessage = "Name is required"
                            }
                        ],
                        Actions =
                        [
                            new ExecuteAction
                            {
                                Title = "Submit",
                                Verb = "submit_name",
                                AssociatedInputs = AssociatedInputs.Auto,
                                Data = new Union<string, SubmitActionData>(new SubmitActionData
                                {
                                    NonSchemaProperties = new Dictionary<string, object?>
                                    {
                                        { "action", "submit_name" }
                                    }
                                })
                            }
                        ]
                    }
                },
                new ShowCardAction
                {
                    Title = "Action ShowCard",
                    Card = new AdaptiveCard
                    {
                        Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                        Body =
                        [
                            new TextBlock("This card's action will show another card")
                        ],
                        Actions =
                        [
                            new ShowCardAction
                            {
                                Title = "Action.ShowCard",
                                Card = new AdaptiveCard
                                {
                                    Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                                    Body =
                                    [
                                        new TextBlock("**Welcome To Your New Card**"),
                                        new TextBlock("This is your new card inside another card")
                                    ]
                                }
                            }
                        ]
                    }
                }
            ]
        };

        return turnContext.SendActivityAsync(
            MessageFactory.Attachment(new Attachment
            {
                ContentType = ContentTypes.AdaptiveCard,
                Content = adaptiveCard
            }),
            cancellationToken);
    }

    public static Task SendToggleVisibilityCardAsync(
        ITeamsTurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var adaptiveCard = new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Body =
            [
                new TextBlock("Click to show or hide the message"),
                new TextBlock("**Hello World!**")
                {
                    Id = "helloWorld",
                    IsVisible = false,
                    Size = TextSize.ExtraLarge
                }
            ],
            Actions =
            [
                new ToggleVisibilityAction
                {
                    Title = "Click me!",
                    TargetElements = new Union<IList<string>, IList<TargetElement>>(
                        new List<string> { "helloWorld" })
                }
            ]
        };

        return turnContext.SendActivityAsync(
            MessageFactory.Attachment(new Attachment
            {
                ContentType = ContentTypes.AdaptiveCard,
                Content = adaptiveCard
            }),
            cancellationToken);
    }
}

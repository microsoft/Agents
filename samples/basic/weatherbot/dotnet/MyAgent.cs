// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Threading;
using System.Threading.Tasks;
using WeatherBot.Agents;

namespace WeatherBot;

public class MyAgent : AgentApplication
{
    private readonly WeatherForecastAgent _weatherAgent;

    public MyAgent(AgentApplicationOptions options, WeatherForecastAgent weatherAgent) : base(options)
    {
        _weatherAgent = weatherAgent ?? throw new ArgumentNullException(nameof(weatherAgent));

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
    }

    protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());

        // Invoke the WeatherForecastAgent to process the message
        WeatherForecastAgentResponse forecastResponse = await _weatherAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory);
        if (forecastResponse == null)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, I couldn't get the weather forecast at the moment."), cancellationToken);
            return;
        }

        // Create a response message based on the response content type from the WeatherForecastAgent
        IActivity response = forecastResponse.ContentType switch
        {
            WeatherForecastAgentResponseContentType.AdaptiveCard => MessageFactory.Attachment(new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = forecastResponse.Content,
            }),
            _ => MessageFactory.Text(forecastResponse.Content),
        };

        // Send the response message back to the user. 
        await turnContext.SendActivityAsync(response, cancellationToken);
    }

    protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome! I'm here to help with all your weather forecast needs!"), cancellationToken);
            }
        }
    }
}
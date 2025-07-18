// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Threading;
using System.Threading.Tasks;
using WeatherAgent.Agents;

namespace WeatherAgent;

public class MyAgent : AgentApplication
{
    private WeatherForecastAgent? _weatherAgent;
    private readonly Kernel _kernel;

    public MyAgent(AgentApplicationOptions options, Kernel kernel) : base(options)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
    }

    protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // Setup local service connection
        ServiceCollection serviceCollection = [
            new ServiceDescriptor(typeof(ITurnState), turnState),
            new ServiceDescriptor(typeof(ITurnContext), turnContext),
            new ServiceDescriptor(typeof(Kernel), _kernel),
        ];

        // Start a Streaming Process 
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working on a response for you", cancellationToken); 

        ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
        _weatherAgent = new WeatherForecastAgent(_kernel, serviceCollection.BuildServiceProvider());

        // Invoke the WeatherForecastAgent to process the message
        WeatherForecastAgentResponse forecastResponse = await _weatherAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory);
        if (forecastResponse == null)
        {
            turnContext.StreamingResponse.QueueTextChunk("Sorry, I couldn't get the weather forecast at the moment.");
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken); 
            return;
        }

        // Create a response message based on the response content type from the WeatherForecastAgent
        // Send the response message back to the user. 
        switch (forecastResponse.ContentType)
        {
            case WeatherForecastAgentResponseContentType.Text:
                turnContext.StreamingResponse.QueueTextChunk(forecastResponse.Content!);
                break;
            case WeatherForecastAgentResponseContentType.AdaptiveCard:
                turnContext.StreamingResponse.FinalMessage = MessageFactory.Attachment(new Attachment()
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = forecastResponse.Content,
                });
                break;
            default:
                break;
        }
        await turnContext.StreamingResponse.EndStreamAsync(cancellationToken); // End the streaming response
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
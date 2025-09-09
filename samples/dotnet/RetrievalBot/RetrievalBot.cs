// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using RetrievalBot.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace RetrievalBot
{
    public class Retrieval: AgentApplication
    {
        private readonly Kernel _kernel;

        public Retrieval(AgentApplicationOptions options, Kernel kernel) : base (options)
        {
            _kernel = kernel;
        }

        [Route(RouteType = RouteType.Activity, Type = ActivityTypes.Message, Rank = RouteRank.Last)]
        protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
            
            RetrievalAgent retrievalAgent = new RetrievalAgent(_kernel, this);

            // Invoke the RetrievalAgent to process the message
            var forecastResponse = await retrievalAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory);
            if (forecastResponse == null)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, I couldn't get the information you are looking for, at the moment."), cancellationToken);
                return;
            }

            // Create a response message based on the response content type from the RetrievalAgent
            IActivity response = forecastResponse.ContentType switch
            {
                RetrievalAgentResponseContentType.AdaptiveCard => MessageFactory.Attachment(new Attachment()
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = forecastResponse.Content,
                }),
                _ => MessageFactory.Text(forecastResponse.Content),
            };

            // Send the response message back to the user. 
            await turnContext.SendActivityAsync(response, cancellationToken);
        }

        [Route(RouteType = RouteType.Conversation, EventName = ConversationUpdateEvents.MembersAdded)]
        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    // welcome the user to the bot
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello! I am Build Genie! I can help you prepare for Build Conference 2025!"), cancellationToken);
                }
            }
        }
    }
}

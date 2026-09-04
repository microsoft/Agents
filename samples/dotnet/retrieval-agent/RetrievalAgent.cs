// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using RetrievalAgent.Services;
using System.Threading;
using System.Threading.Tasks;

namespace RetrievalAgent
{
    public class Retrieval : AgentApplication
    {
        private readonly IBuildGenieMessageRoute _messageRoute;

        public Retrieval(AgentApplicationOptions options, IBuildGenieMessageRoute messageRoute) : base(options)
        {
            _messageRoute = messageRoute;
            UserAuthorization.OnUserSignInFailure(OnUserSignInFailureAsync);
        }

        [MessageRoute]
        protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            await _messageRoute.HandleAsync(
                turnContext.Activity.Text ?? string.Empty,
                _ => UserAuthorization.GetTurnTokenAsync(turnContext, "graph"),
                (text, token) => turnContext.SendActivityAsync(MessageFactory.Text(text), token),
                (activity, token) => turnContext.SendActivityAsync(activity, token),
                cancellationToken);
        }

        [MembersAddedRoute]
        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello! I am Build Genie. Ask me about Build 2025 sessions in the configured SharePoint site. I only search content you can access."), cancellationToken);
                }
            }
        }

        private static Task OnUserSignInFailureAsync(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken) =>
            turnContext.SendActivityAsync(MessageFactory.Text(BuildGenieResponses.For(RetrievalStatus.NotSignedIn)), cancellationToken);
    }
}

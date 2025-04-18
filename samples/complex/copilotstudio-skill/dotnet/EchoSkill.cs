// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;

namespace CopilotStudioEchoSkill;

public class EchoSkill : AgentApplication
{
    public EchoSkill(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.EndOfConversation, EndOfConversationAsync);

        // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
        OnActivity(ActivityTypes.Message, OnMessageAsync);

        // Handle uncaught exceptions be resetting ConversationState and letting MCS know the conversation is over.
        OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
        {
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);

            var eoc = Activity.CreateEndOfConversationActivity();
            eoc.Code = EndOfConversationCodes.Error;
            eoc.Text = exception.Message;
            await turnContext.SendActivityAsync(eoc, cancellationToken);
        });
    }

    protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync("Hi, This is EchoSkill", cancellationToken: cancellationToken);
            }
        }
    }

    protected async Task EndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // This will be called if MCS is ending the conversation.  Sending additional messages should be
        // avoided as the conversation may have been deleted.
        // Perform cleanup of resources if needed.
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
    }

    protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Text.Contains("end"))
        {
            await turnContext.SendActivityAsync("(EchoSkill) Ending conversation...", cancellationToken: cancellationToken);

            // Indicate this conversation is over by sending an EndOfConversation Activity.
            // This Agent doesn't return a value, but if it did it could be put in Activity.Value.
            var endOfConversation = Activity.CreateEndOfConversationActivity();
            endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
            await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync($"(EchoSkill): {turnContext.Activity.Text}", cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("(EchoSkill): Say \"end\" and I'll end the conversation.", cancellationToken: cancellationToken);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Proactive;

public class ProactiveAgent : AgentApplication
{
    public ProactiveAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Manual way to store a conversation for use in Proactive.
        OnMessage("-s", async (turnContext, turnState, cancellationToken) =>
        {
            var id = await Proactive.StoreConversationAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync($"Conversation '{id}' stored", cancellationToken: cancellationToken);
        });

        // Send the Conversation JSON to the chat
        OnMessage("-convo", async (turnContext, turnState, cancellationToken) =>
        {
            var conversation = new Conversation(turnContext);
            await turnContext.SendActivityAsync(ProtocolJsonSerializer.ToJson(conversation), cancellationToken: cancellationToken);
        });

        // In-code ContinueConversation using a stored Conversation.  Send "-s" first to store the conversation.
        OnMessage(new Regex("-c.*"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ');
            var conversationId = split.Length == 1 ? turnContext.Activity.Conversation.Id : split[1];

            await Proactive.ContinueConversationAsync(turnContext.Adapter, conversationId, OnContinueConversationAsync, cancellationToken: cancellationToken);
        });

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    public async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // This demonstrates using a Conversation instance to perform ContinueConversation with a custom 
        // continuation activity.
        // This does the same as:  await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}"),
        // except using ContinueConversation.
        // ConversationBuilder can also be used to manually create a Conversation instance manually.
        var conversation = new Conversation(turnContext);

        var customContinuation = conversation.Reference.GetContinuationActivity();
        customContinuation.Value = turnContext.Activity;

        await Proactive.ContinueConversationAsync(
            turnContext.Adapter, 
            conversation, 
            async (context, state, ct) =>
            {
                var originalActivity = (IActivity)context.Activity.Value;
                await context.SendActivityAsync($"You said: {originalActivity.Text}", cancellationToken: ct);
            },
            continuationActivity: customContinuation,
            cancellationToken: cancellationToken);
    }

    // This attribute indicates this is a ContinueConversation handler.
    // It can be used in a code-first approach using Proactive.ContinueConversationAsync, or if MapAgentProactiveEndpoints was called in
    // startup it can be mapped to an Http request to /proactive/continue that triggers this logic.
    [ContinueConversation]
    public async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"This is OnContinueConversation", cancellationToken: cancellationToken);
    }
}

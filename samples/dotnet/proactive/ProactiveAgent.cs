// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
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
            var id = await Proactive.StoreConversationAsync(new Conversation(turnContext), cancellationToken);
            await turnContext.SendActivityAsync(
                $"Your conversation has been stored. Send a POST request to /proactive/sendActivity/{id} to trigger a proactive message.",
                cancellationToken: cancellationToken);
        });

        // Send the Conversation JSON to the chat
        OnMessage("-convo", async (turnContext, turnState, cancellationToken) =>
        {
            var conversation = new Conversation(turnContext);
            await turnContext.SendActivityAsync(ProtocolJsonSerializer.ToJson(conversation), cancellationToken: cancellationToken);
        });

        // Continue the current conversation, or a stored conversation when an ID is supplied.
        OnMessage(new Regex(@"^-c(?:\s+\S+)?\s*$"), async (turnContext, turnState, cancellationToken) =>
        {
            var split = turnContext.Activity.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Conversation? conversation;

            if (split.Length == 1)
            {
                conversation = new Conversation(turnContext);
            }
            else
            {
                var conversationId = split[1];
                conversation = await Proactive.GetConversationAsync(conversationId, cancellationToken);

                if (conversation == null)
                {
                    await turnContext.SendActivityAsync(
                        $"Conversation '{conversationId}' was not found. Send -s first to store it.",
                        cancellationToken: cancellationToken);
                    return;
                }
            }

            await Proactive.ContinueConversationAsync(
                turnContext.Adapter,
                conversation,
                OnContinueConversationAsync,
                cancellationToken: cancellationToken);
        });

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    public async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(CreateWelcomeMessage(), cancellationToken);
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

    private static IActivity CreateWelcomeMessage()
    {
        return MessageFactory.Attachment(new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = new
            {
                type = "AdaptiveCard",
                version = "1.5",
                body = new object[]
                {
                    new { type = "TextBlock", text = "Welcome to the Proactive sample.", weight = "Bolder", size = "Medium", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "Commands:", weight = "Bolder", spacing = "Medium", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "• -s: Store this conversation.", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "• -c: Continue this conversation proactively.", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "• -c <conversation-id>: Continue a stored conversation.", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "• -convo: Show the conversation data for the HTTP example.", wrap = true, horizontalAlignment = "Left" },
                    new { type = "TextBlock", text = "Send other text to echo it from a proactive turn.", spacing = "Medium", wrap = true, horizontalAlignment = "Left" }
                }
            }
        });
    }

    // This attribute indicates this is a ContinueConversation handler.
    // It can be used in a code-first approach using Proactive.ContinueConversationAsync, or if MapAgentProactiveEndpoints was called in
    // startup it can be mapped to an Http request to /proactive/continue that triggers this logic.
    [ContinueConversation]
    public async Task OnContinueConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync("This is OnContinueConversation", cancellationToken: cancellationToken);
    }
}

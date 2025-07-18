// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingMessageAgent;

public class StreamingAgent : AgentApplication
{
    private readonly ChatClient _chatClient;

    /// <summary>
    /// Example of a streaming response agent using the Azure OpenAI ChatClient.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="chatClient"></param>
    public StreamingAgent(AgentApplicationOptions options, ChatClient chatClient) : base(options)
    {
        _chatClient = chatClient;

        // Register an event to welcome new channel members.
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Register an event to handle messages from the client.
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    /// <summary>
    /// Send a welcome message to the user when they join the conversation.
    /// </summary>
    /// <param name="turnContext"></param>
    /// <param name="turnState"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Say anything and I'll recite poetry."), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Handle Messages events from clients. 
    /// </summary>
    /// <param name="turnContext"></param>
    /// <param name="turnState"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            // Raise an informative update to the calling client,  if the client support StreamingResponses this will appear as a contextual notification. 
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Hold on for an awesome poem about Apollo...", cancellationToken);

            // Setup system messages and the user request,
            // Normally we would use the turnState to manage this list in context of the conversation and add to it as the conversation proceeded 
            // And Normally the user Chat Message would be provided by the incoming message,
            // However for our purposes we are hardcoding the UserMessage. 
            List<ChatMessage> messages =
            [
                new SystemChatMessage("""
                    You are a creative assistant who has deeply studied Greek and Roman Gods, You also know all of the Percy Jackson Series
                    You write poems about the Greek Gods as they are depicted in the Percy Jackson books.
                    You format the poems in a way that is easy to read and understand
                    You break your poems into stanzas 
                    You format your poems in Markdown using double lines to separate stanzas
                    """),

                new UserChatMessage("Write a poem about the Greek God Apollo as depicted in the Percy Jackson books"),
            ];

            // Requesting the connected LLM Model to do work :) 
            await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(
                messages,
                new ChatCompletionOptions { MaxOutputTokenCount = 1000 },
                cancellationToken: cancellationToken))
            {
                if (update.ContentUpdate.Count > 0)
                {
                    if (!string.IsNullOrEmpty(update.ContentUpdate[0]?.Text))
                        turnContext.StreamingResponse.QueueTextChunk(update.ContentUpdate[0]?.Text!);
                }
            }
        }
        finally
        {
            // Signal that your done with this stream. 
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
        }
    }
}
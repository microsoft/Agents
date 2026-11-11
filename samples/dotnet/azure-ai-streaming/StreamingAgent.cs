// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AzureAIStreaming;

public class StreamingAgent : AgentApplication
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<StreamingAgent> _logger;

    /// <summary>
    /// Example of a streaming response agent using the Azure OpenAI ChatClient.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="chatClient"></param>
    /// <param name="logger"></param>
    public StreamingAgent(AgentApplicationOptions options, ChatClient chatClient, ILogger<StreamingAgent> logger) : base(options)
    {
        _chatClient = chatClient;
        _logger = logger;

        // Register an event to welcome new channel members.
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Register an event to handle feedback submitted for a streamed response.
        OnFeedbackLoop(OnFeedbackAsync);

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
                break;
            }
        }
    }

    /// <summary>
    /// Handle feedback submitted for a streamed response.
    /// </summary>
    private async Task OnFeedbackAsync(ITurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Feedback received: {@Feedback}", feedbackData.ActionValue);
        await turnContext.SendActivityAsync(MessageFactory.Text("Thanks for submitting your feedback."), cancellationToken);
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
        turnContext.StreamingResponse.FeedbackLoopEnabled = true;
        turnContext.StreamingResponse.EnableGeneratedByAILabel = true;
        turnContext.StreamingResponse.SensitivityLabel = new SensitivityUsageInfo
        {
            Type = "https://schema.org/Message",
            AtType = "CreativeWork",
            Name = "Internal"
        };

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
                    You are a creative assistant who has deeply studied Greek and Roman gods and the Percy Jackson series.
                    You write poems about the Greek gods as they are depicted in the Percy Jackson books.
                    You format the poems in a way that is easy to read and understand.
                    You break your poems into stanzas.
                    You format your poems in Markdown using blank lines to separate stanzas.
                    """),

                new UserChatMessage("Write a poem of about 500 words about the Greek god Apollo as depicted in the Percy Jackson books."),
            ];

            // Requesting the connected LLM Model to do work :) 
            await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(
                messages,
                new ChatCompletionOptions(),
                cancellationToken: cancellationToken))
            {
                if (update.ContentUpdate.Count > 0)
                {
                    if (!string.IsNullOrEmpty(update.ContentUpdate[0]?.Text))
                        turnContext.StreamingResponse.QueueTextChunk(update.ContentUpdate[0]?.Text!);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Streaming was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error during streaming.");
            turnContext.StreamingResponse.QueueTextChunk("An error occurred while generating the poem. Please try again later.");
        }
        finally
        {
            // Signal that your done with this stream. 
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
        }
    }
}

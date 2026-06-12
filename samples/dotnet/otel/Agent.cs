// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Otel;

public class Agent : AgentApplication
{
    private readonly ILogger<Agent> _logger;

    public Agent(AgentApplicationOptions options, ILogger<Agent> logger) : base(options)
    {
        _logger = logger;
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        OnTurnError(OnTurnError);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        using System.Diagnostics.Activity? activity = AgentTelemetry.ActivitySource.StartActivity("agent.welcome_message");

        activity?.SetTag("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown");
        activity?.SetTag("channel.id", turnContext.Activity.ChannelId ?? "unknown");
        activity?.SetTag("members.added.count", turnContext.Activity.MembersAdded.Count);

        try
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    activity?.AddEvent(new ActivityEvent("member.added", timestamp: DateTime.UtcNow, tags: new ActivityTagsCollection
                    {
                        { "member.id", member.Id },
                        { "member.name", member.Name }
                    }));
                }
            }

            await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);

            AgentTelemetry.RouteExecutedCounter.Add(1,
            [
                new("route.type", "welcome_message"),
                new("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown")
            ]);

            _logger.LogInformation(
                "Welcome message sent for conversation {ConversationId}",
                turnContext.Activity.Conversation?.Id ?? "unknown");

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));
            _logger.LogError(ex, "Welcome message failed for conversation {ConversationId}", turnContext.Activity.Conversation?.Id ?? "unknown");
            throw;
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        using System.Diagnostics.Activity? activity = AgentTelemetry.ActivitySource.StartActivity("agent.message_handler");
        Stopwatch stopwatch = Stopwatch.StartNew();
        string conversationId = turnContext.Activity.Conversation?.Id ?? "unknown";
        string channelId = turnContext.Activity.ChannelId ?? "unknown";
        string status = "success";

        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("channel.id", channelId);
        activity?.SetTag("message.text.length", turnContext.Activity.Text?.Length ?? 0);
        activity?.SetTag("user.id", turnContext.Activity.From?.Id ?? "unknown");
        activity?.AddEvent(new ActivityEvent("message.received", timestamp: DateTime.UtcNow, tags: new ActivityTagsCollection
        {
            { "message.id", turnContext.Activity.Id },
            { "message.text", turnContext.Activity.Text },
            { "user.id", turnContext.Activity.From?.Id },
            { "channel.id", channelId }
        }));

        try
        {
            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);

            activity?.AddEvent(new ActivityEvent("response.sent", timestamp: DateTime.UtcNow));

            AgentTelemetry.RouteExecutedCounter.Add(1, 
            [
                new("route.type", "message_handler"),
                new("conversation.id", conversationId)
            ]);

            _logger.LogInformation(
                "Message handled for conversation {ConversationId}", conversationId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));
            _logger.LogError(ex, "Message handling failed for conversation {ConversationId}", turnContext.Activity.Conversation?.Id ?? "unknown");
            status = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            AgentTelemetry.MessageProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
            [
                new("conversation.id", conversationId),
                new("channel.id", channelId),
                new("status", status)
            ]);
        }
    }

    private async Task OnTurnError(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception in conversation {ConversationId}", turnContext.Activity.Conversation?.Id ?? "unknown");
        // Send a message to the user
        await turnContext.SendActivityAsync("The bot encountered an error or bug.", cancellationToken: cancellationToken); 
    }
}

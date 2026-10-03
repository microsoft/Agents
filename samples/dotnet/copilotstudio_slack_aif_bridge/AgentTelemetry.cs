// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Slack_MCS_Bridge;

public static class AgentTelemetry
{
    public const string ServiceName = "Slack_MCS_Bridge";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    public static readonly Counter<long> RouteExecutedCounter = Meter.CreateCounter<long>(
        "agent.routes.executed.count",
        unit: "routes",
        description: "Number of routes executed by the agent");

    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
        "agent.message.processing.duration",
        unit: "ms",
        description: "Duration of message processing in milliseconds");


    public static async Task<T> InvokeObservedAgentOperation<T>(string operationName, ITurnContext context, Func<Task<T>> func)
    {
        // Init the activity for observability
        using var activity = InitializeMessageHandlingActivity(operationName, context);
        var routeStopwatch = Stopwatch.StartNew();
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
            throw;
        }
        finally
        {
            routeStopwatch.Stop();
            FinalizeMessageHandlingActivity(activity, context, routeStopwatch.ElapsedMilliseconds, true);
        }
    }
    public static Activity InitializeMessageHandlingActivity(string handlerName, ITurnContext context)
    {
System.Diagnostics.Activity? activity = AgentTelemetry.ActivitySource.StartActivity(handlerName);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string conversationId = context.Activity.Conversation?.Id ?? "unknown";
        string channelId = context.Activity.ChannelId.ToString() ?? "unknown";
        string status = "success";

        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("channel.id", channelId);
        activity?.SetTag("message.text.length", context.Activity.Text?.Length ?? 0);
        activity?.SetTag("user.id", context.Activity.From?.Id ?? "unknown");
        activity?.AddEvent(new ActivityEvent("message.received", timestamp: DateTime.UtcNow, tags: new ActivityTagsCollection
        {
            { "message.id", context.Activity.Id },
            { "message.text", context.Activity.Text },
            { "user.id", context.Activity.From?.Id },
            { "channel.id", channelId }
        }));
        return activity!;
    }

    public static void FinalizeMessageHandlingActivity(Activity activity, ITurnContext context, long duration, bool success)
    {

        RouteExecutedCounter.Add(1,
            new("Route.Type", "message_handler"),
            new("Conversation.Id", context.Activity.Conversation?.Id ?? "unknown"));

        AgentTelemetry.MessageProcessingDuration.Record(duration,
        [
            new("conversation.id", context.Activity.Conversation?.Id ?? "unknown"),
                new("channel.id", context.Activity.ChannelId?.ToString() ?? "unknown"),
                new("status", success)
        ]);

        if (success)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }
        activity?.Stop();
        activity?.Dispose();
    }
}

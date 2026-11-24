using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Slack_MCS_Bridge.Tests;

public class AgentTelemetryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinalizesWithActualOperationStatus(bool succeeds)
    {
        string operationName = $"telemetry-test-{Guid.NewGuid()}";
        var context = new Mock<ITurnContext>();
        context.SetupGet(c => c.Activity).Returns(new Microsoft.Agents.Core.Models.Activity
        {
            Conversation = new ConversationAccount { Id = operationName },
            ChannelId = "test"
        });

        System.Diagnostics.Activity? stoppedActivity = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentTelemetry.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == operationName)
                    stoppedActivity = activity;
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        var metricStatuses = new List<bool>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentTelemetry.ServiceName
                    && instrument.Name == "agent.message.processing.duration")
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            bool matchesOperation = false;
            bool? status = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "conversation.id" && Equals(tag.Value, operationName))
                    matchesOperation = true;
                if (tag.Key == "status")
                    status = Assert.IsType<bool>(tag.Value);
            }
            if (matchesOperation)
            {
                Assert.NotNull(status);
                metricStatuses.Add(status.Value);
            }
        });
        meterListener.Start();

        var failure = new InvalidOperationException("Operation failed");
        async Task<int> Operation()
        {
            await Task.Yield();
            if (!succeeds)
                throw failure;
            return 42;
        }

        if (succeeds)
        {
            Assert.Equal(42, await AgentTelemetry.InvokeObservedAgentOperation(operationName, context.Object, Operation));
        }
        else
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AgentTelemetry.InvokeObservedAgentOperation(operationName, context.Object, Operation));
            Assert.Same(failure, thrown);
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal(succeeds ? ActivityStatusCode.Ok : ActivityStatusCode.Error, stoppedActivity.Status);
        Assert.Equal(succeeds, Assert.Single(metricStatuses));
        if (!succeeds)
            Assert.Contains(stoppedActivity.Events, e => e.Name == "exception");
    }
}

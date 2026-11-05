using System.Reflection;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Slack_MCS_Bridge.Tests;

public class MessageRouteTests
{
    [Fact]
    public async Task NonSlackWaitsForSharedProcessingBeforeStreamingTheAnswer()
    {
        var completion = new TaskCompletionSource<CopilotStudioResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new TestBridge(completion.Task);
        var streaming = new Mock<IStreamingResponse>();
        var context = new Mock<ITurnContext>(MockBehavior.Strict);
        context.SetupGet(c => c.StreamingResponse).Returns(streaming.Object);
        var state = Mock.Of<ITurnState>();
        using var cancellation = new CancellationTokenSource();

        Task handling = InvokeNonSlackRoute(agent, context.Object, state, cancellation.Token);

        Assert.Equal(1, agent.ProcessingCalls);
        Assert.False(handling.IsCompleted);
        streaming.VerifyNoOtherCalls();
        completion.SetResult(new CopilotStudioResponse("Complete answer", []));
        await handling;

        Assert.Same(context.Object, agent.ReceivedContext);
        Assert.Null(agent.ReceivedAdditionalData);
        Assert.Equal(cancellation.Token, agent.ReceivedCancellationToken);
        streaming.Verify(s => s.QueueTextChunk("Complete answer"), Times.Once);
        streaming.Verify(s => s.EndStreamAsync(cancellation.Token), Times.Once);
        streaming.VerifyNoOtherCalls();
        context.VerifyGet(c => c.StreamingResponse, Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessingFailureDoesNotSendAnEchoOrPartialAnswer()
    {
        var agent = new TestBridge(Task.FromException<CopilotStudioResponse>(new InvalidOperationException("Copilot failed")));
        var context = new Mock<ITurnContext>(MockBehavior.Strict);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeNonSlackRoute(agent, context.Object, Mock.Of<ITurnState>(), CancellationToken.None));

        context.VerifyNoOtherCalls();
    }

    private static Task InvokeNonSlackRoute(
        McsSlackBridge agent, ITurnContext context, ITurnState state, CancellationToken cancellationToken)
    {
        MethodInfo method = typeof(McsSlackBridge).GetMethod("OnMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(agent, [context, state, cancellationToken])!;
    }

    private sealed class TestBridge(Task<CopilotStudioResponse> response) : McsSlackBridge(
        new AgentApplicationOptions(new MemoryStorage(), NullLoggerFactory.Instance),
        Mock.Of<IChatClient>(),
        new ConfigurationBuilder().Build(),
        new ServiceCollection().BuildServiceProvider(),
        NullLogger<McsSlackBridge>.Instance)
    {
        public int ProcessingCalls { get; private set; }
        public ITurnContext? ReceivedContext { get; private set; }
        public ITurnState? ReceivedState { get; private set; }
        public object? ReceivedAdditionalData { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        protected override Task<CopilotStudioResponse> ProcessCopilotStudioResponseAsync(
            ITurnContext context, ITurnState turnState, object additionalData, CancellationToken cancellationToken)
        {
            ProcessingCalls++;
            ReceivedContext = context;
            ReceivedState = turnState;
            ReceivedAdditionalData = additionalData;
            ReceivedCancellationToken = cancellationToken;
            return response;
        }
    }
}

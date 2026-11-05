using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Slack_MCS_Bridge.Tests;

public class CopilotStudioResponseTests
{
    [Fact]
    public async Task CollectsFinalTextWithoutRepeatingStreamingSnapshots()
    {
        var response = await CopilotStudioResponse.CollectAsync(Activities(
            CreateActivity(ActivityTypes.Typing, "Hel", StreamTypes.Streaming),
            CreateActivity(ActivityTypes.Typing, "Hello", StreamTypes.Streaming),
            CreateActivity(ActivityTypes.Message, "Hello world", StreamTypes.Final)));

        Assert.Equal("Hello world", response.Text);
        Assert.Empty(response.Attachments);
    }

    [Fact]
    public async Task PreservesSeparateMessagesIncludingRepeatedText()
    {
        var response = await CopilotStudioResponse.CollectAsync(Activities(
            CreateActivity(ActivityTypes.Message, "First"),
            CreateActivity(ActivityTypes.Message, "Answer", StreamTypes.Final),
            CreateActivity(ActivityTypes.Message, "Answer", StreamTypes.Final)));

        Assert.Equal("First\n\nAnswer\n\nAnswer", response.Text);
    }

    [Fact]
    public async Task CollectsOriginalAttachmentsFromPartialFinalAndOrdinaryActivitiesInOrder()
    {
        var card = new Attachment { ContentType = "application/vnd.microsoft.card.adaptive", Content = new { type = "AdaptiveCard" } };
        var image = new Attachment { ContentType = "image/png", ContentUrl = "https://example.com/image.png" };
        var file = new Attachment { ContentType = "application/pdf", Name = "report.pdf" };
        var partial = CreateActivity(ActivityTypes.Typing, "partial", StreamTypes.Streaming);
        partial.Attachments = [card];
        var final = CreateActivity(ActivityTypes.Message, "Answer", StreamTypes.Final);
        final.Attachments = [image];
        var ordinary = CreateActivity(ActivityTypes.Message, null);
        ordinary.Attachments = [file];

        var response = await CopilotStudioResponse.CollectAsync(Activities(partial, final, ordinary));

        Assert.Equal("Answer", response.Text);
        Assert.Collection(response.Attachments,
            attachment => Assert.Same(card, attachment),
            attachment => Assert.Same(image, attachment),
            attachment => Assert.Same(file, attachment));
    }

    [Fact]
    public async Task ForwardsInformativeUpdatesWithoutAddingThemToAnswer()
    {
        var updates = new List<string>();
        var response = await CopilotStudioResponse.CollectAsync(
            Activities(
                CreateActivity(ActivityTypes.Typing, "Searching", StreamTypes.Informative),
                CreateActivity(ActivityTypes.Typing, "Typing without stream metadata"),
                CreateActivity(ActivityTypes.Message, "Answer")),
            (text, _) =>
            {
                updates.Add(text);
                return Task.CompletedTask;
            });

        Assert.Equal(["Searching"], updates);
        Assert.Equal("Answer", response.Text);
    }

    [Fact]
    public async Task SupportsNullStreamTypeAndCaseInsensitiveFinal()
    {
        var noType = CreateActivity(ActivityTypes.Message, "First");
        noType.Entities = [new StreamInfo { StreamType = null! }];

        var response = await CopilotStudioResponse.CollectAsync(Activities(
            noType, CreateActivity(ActivityTypes.Message, "Second", "FINAL")));

        Assert.Equal("First\n\nSecond", response.Text);
    }

    [Fact]
    public async Task EmptyActivitiesDoNotProduceTextOrAttachments()
    {
        var response = await CopilotStudioResponse.CollectAsync(Activities(
            CreateActivity(ActivityTypes.Event, "Not an answer"),
            CreateActivity(ActivityTypes.Message, null)));

        Assert.Empty(response.Text);
        Assert.Empty(response.Attachments);
    }

    [Fact]
    public async Task CancellationStopsCollection()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CopilotStudioResponse.CollectAsync(
                Activities(CreateActivity(ActivityTypes.Message, "Answer")),
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task SourceFailureIsNotReturnedAsPartialSuccess()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopilotStudioResponse.CollectAsync(FailingActivities()));
    }

    [Fact]
    public async Task StreamsBufferedTextAndOriginalAttachmentsThenEndsStream()
    {
        var attachment = new Attachment { ContentType = "image/png" };
        var response = await CopilotStudioResponse.CollectAsync(Activities(
            new Activity { Type = ActivityTypes.Message, Text = "Answer", Attachments = [attachment] }));
        var streaming = new Mock<IStreamingResponse>();
        var calls = new List<string>();
        streaming.Setup(s => s.QueueTextChunk("Answer")).Callback(() => calls.Add("text"));
        streaming.Setup(s => s.AddAttachment(attachment)).Callback(() => calls.Add("attachment"));
        streaming.Setup(s => s.EndStreamAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("end"));

        await response.StreamAsync(streaming.Object, CancellationToken.None);

        Assert.Equal(["text", "attachment", "end"], calls);
        streaming.Verify(s => s.EndStreamAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AttachmentOnlyResponseStillEndsStream()
    {
        var attachment = new Attachment { ContentType = "application/vnd.microsoft.card.adaptive" };
        var response = await CopilotStudioResponse.CollectAsync(Activities(
            new Activity { Type = ActivityTypes.Message, Attachments = [attachment] }));
        var streaming = new Mock<IStreamingResponse>();

        await response.StreamAsync(streaming.Object, CancellationToken.None);

        streaming.Verify(s => s.QueueTextChunk(It.IsAny<string>()), Times.Never);
        streaming.Verify(s => s.AddAttachment(attachment), Times.Once);
        streaming.Verify(s => s.EndStreamAsync(CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(StreamingResponseResult.Timeout)]
    [InlineData(StreamingResponseResult.Error)]
    [InlineData(StreamingResponseResult.AlreadyEnded)]
    [InlineData(StreamingResponseResult.NotStarted)]
    public async Task FailedStreamCompletionIsSurfaced(StreamingResponseResult result)
    {
        var response = new CopilotStudioResponse("Answer", []);
        var streaming = new Mock<IStreamingResponse>();
        streaming.Setup(s => s.EndStreamAsync(CancellationToken.None)).ReturnsAsync(result);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            response.StreamAsync(streaming.Object, CancellationToken.None));

        Assert.Contains(result.ToString(), exception.Message);
    }

    [Fact]
    public async Task CancelledStreamCompletionIsSurfacedAsCancellation()
    {
        var response = new CopilotStudioResponse("Answer", []);
        var streaming = new Mock<IStreamingResponse>();
        streaming.Setup(s => s.EndStreamAsync(CancellationToken.None))
            .ReturnsAsync(StreamingResponseResult.UserCancelled);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            response.StreamAsync(streaming.Object, CancellationToken.None));
    }

    [Fact]
    public async Task CancelledDeliveryDoesNotQueueAnAnswer()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var streaming = new Mock<IStreamingResponse>(MockBehavior.Strict);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CopilotStudioResponse("Answer", []).StreamAsync(streaming.Object, cancellation.Token));

        streaming.VerifyNoOtherCalls();
    }

    private static Activity CreateActivity(string type, string? text, string? streamType = null)
    {
        return new Activity
        {
            Type = type,
            Text = text,
            Entities = streamType is null ? [] : [new StreamInfo { StreamType = streamType }]
        };
    }

    private static async IAsyncEnumerable<IActivity> Activities(params IActivity[] activities)
    {
        foreach (var activity in activities)
        {
            await Task.Yield();
            yield return activity;
        }
    }

    private static async IAsyncEnumerable<IActivity> FailingActivities()
    {
        await Task.Yield();
        yield return CreateActivity(ActivityTypes.Message, "Partial answer");
        throw new InvalidOperationException("Copilot Studio failed");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Slack_MCS_Bridge.Tests;

public class SlackDeliveryTests
{
    private const string ValidContent = """
        {"text":"Converted answer","blocks":[{"type":"section","text":{"type":"mrkdwn","text":"*Converted answer*"}}]}
        """;

    [Fact]
    public async Task ConvertsTextAndOriginalAttachmentsThenPostsWithContextRoutingAndSavesSession()
    {
        var attachment = new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Name = "Details",
            Content = new { type = "AdaptiveCard", body = new[] { new { type = "TextBlock", text = "Card details" } } }
        };
        var response = new CopilotStudioResponse("Original answer", [attachment]);
        using var test = new Harness(response, ValidContent);

        await test.RunAsync();

        Assert.Equal(1, test.Agent.ProcessingCalls);
        Assert.Same(response, test.Temp.GetValue<CopilotStudioResponse>(McsSlackBridge.CopilotStudioResponseStateKey));
        var input = test.ModelInputs.Single().Last(m => m.Role == ChatRole.User).Text;
        using var inputJson = JsonDocument.Parse(input);
        Assert.Equal("Original answer", inputJson.RootElement.GetProperty("text").GetString());
        Assert.Equal("Card details", inputJson.RootElement.GetProperty("attachments")[0]
            .GetProperty("content").GetProperty("body")[0].GetProperty("text").GetString());
        Assert.DoesNotContain("test-token", input);
        Assert.IsType<ChatResponseFormatJson>(test.Options.Single()!.ResponseFormat);
        Assert.Contains("Slack", test.Options.Single()!.Instructions);

        using var posted = JsonDocument.Parse(Assert.Single(test.Transport.Bodies));
        Assert.Equal("C-context", posted.RootElement.GetProperty("channel").GetString());
        Assert.Equal("123.456", posted.RootElement.GetProperty("thread_ts").GetString());
        Assert.Equal("Converted answer", posted.RootElement.GetProperty("text").GetString());
        Assert.Equal("section", posted.RootElement.GetProperty("blocks")[0].GetProperty("type").GetString());
        Assert.Equal("Bearer test-token", Assert.Single(test.Transport.AuthorizationHeaders));
        Assert.EndsWith("/chat.postMessage", Assert.Single(test.Transport.Urls));
        Assert.False(string.IsNullOrEmpty(test.Conversation.GetValue<string>("conversation.threadInfo")));
        test.Context.As<ITurnContext>().VerifyGet(c => c.StreamingResponse, Times.Never);
    }

    [Fact]
    public async Task RestoresSessionOnTheNextTurn()
    {
        using var test = new Harness(new CopilotStudioResponse("Original answer", []), ValidContent);

        await test.RunAsync();
        await test.RunAsync();

        Assert.Equal(2, test.ModelInputs.Count);
        Assert.Contains(test.ModelInputs[1], m => m.Role == ChatRole.Assistant && m.Text == ValidContent);
        Assert.Equal(2, test.Transport.Bodies.Count);
    }

    [Fact]
    public async Task AttachmentOnlyResponseIsConverted()
    {
        using var test = new Harness(new CopilotStudioResponse("", [
            new Attachment { ContentType = "application/pdf", ContentUrl = "https://example.com/report.pdf" }
        ]), ValidContent);

        await test.RunAsync();

        Assert.Single(test.Transport.Bodies);
        Assert.Contains("report.pdf", test.ModelInputs.Single().Last(m => m.Role == ChatRole.User).Text);
    }

    [Fact]
    public async Task DoesNotLetTheModelOverrideDestinationThreadOrIdentity()
    {
        string content = ValidContent.Replace("\"text\":\"Converted answer\"", """
            "channel":"C-other","thread_ts":"999.000","token":"bad","username":"somebody","text":"Converted answer"
            """);
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content, threadTs: null);

        await test.RunAsync();

        using var posted = JsonDocument.Parse(Assert.Single(test.Transport.Bodies));
        Assert.Equal("C-context", posted.RootElement.GetProperty("channel").GetString());
        Assert.False(posted.RootElement.TryGetProperty("thread_ts", out _));
        Assert.False(posted.RootElement.TryGetProperty("token", out _));
        Assert.False(posted.RootElement.TryGetProperty("username", out _));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("```json\n{}\n```")]
    [InlineData("{}")]
    [InlineData("{\"text\":\"Answer\",\"blocks\":[]}")]
    [InlineData("{\"text\":\"Answer\",\"blocks\":[{\"type\":\"unknown\"}]}")]
    [InlineData("{\"text\":\"Answer\",\"blocks\":[{\"type\":\"section\"}]}")]
    public async Task RejectsInvalidModelOutputWithoutPostingOrSaving(string content)
    {
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content);

        await Assert.ThrowsAnyAsync<Exception>(() => test.RunAsync());

        Assert.Single(test.ModelInputs);
        Assert.Empty(test.Transport.Bodies);
        Assert.Null(test.Conversation.GetValue<string?>("conversation.threadInfo", () => null));
    }

    [Fact]
    public async Task SlackApiFailureIsSurfacedAndDoesNotAdvanceSavedSession()
    {
        using var test = new Harness(new CopilotStudioResponse("Answer", []), ValidContent);
        await test.RunAsync();
        string previousSession = test.Conversation.GetValue<string>("conversation.threadInfo");
        test.Transport.Response = """{"ok":false,"error":"invalid_blocks"}""";

        var error = await Assert.ThrowsAsync<SlackResponseException>(() => test.RunAsync());

        Assert.Contains("invalid_blocks", error.Message);
        Assert.Equal(previousSession, test.Conversation.GetValue<string>("conversation.threadInfo"));
    }

    [Fact]
    public async Task ModelFailureIsSurfacedWithoutPosting()
    {
        using var test = new Harness(new CopilotStudioResponse("Answer", []), ValidContent);
        test.ChatClient.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Model unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => test.RunAsync());

        Assert.Empty(test.Transport.Bodies);
    }

    [Fact]
    public async Task EmptyResponseDoesNotGenerateOrPostInventedContent()
    {
        using var test = new Harness(new CopilotStudioResponse("", []), ValidContent);

        await test.RunAsync();

        Assert.Empty(test.ModelInputs);
        Assert.Empty(test.Transport.Bodies);
    }

    [Fact]
    public async Task CancellationIsPassedToTheModelAndPreventsPosting()
    {
        using var test = new Harness(new CopilotStudioResponse("Answer", []), ValidContent);
        using var cancellation = new CancellationTokenSource();
        test.ChatClient.Setup(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken token) =>
            {
                Assert.Equal(cancellation.Token, token);
                cancellation.Cancel();
                return Task.FromCanceled<ChatResponse>(token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.RunAsync(cancellation.Token));

        Assert.Empty(test.Transport.Bodies);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task EnforcesSlackMessageBlockLimit(int blockCount, bool valid)
    {
        string content = JsonSerializer.Serialize(new
        {
            text = "Answer",
            blocks = Enumerable.Repeat(new { type = "divider" }, blockCount)
        });
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content);

        if (valid)
        {
            await test.RunAsync();
            using var posted = JsonDocument.Parse(Assert.Single(test.Transport.Bodies));
            Assert.Equal(blockCount, posted.RootElement.GetProperty("blocks").GetArrayLength());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.RunAsync());
            Assert.Empty(test.Transport.Bodies);
        }
    }

    [Theory]
    [InlineData("section", "mrkdwn", 3000, true)]
    [InlineData("section", "mrkdwn", 3001, false)]
    [InlineData("header", "plain_text", 150, true)]
    [InlineData("header", "plain_text", 151, false)]
    [InlineData("header", "mrkdwn", 10, false)]
    public async Task EnforcesTextLimitsWithoutTruncating(string blockType, string textType, int length, bool valid)
    {
        string content = JsonSerializer.Serialize(new
        {
            text = "Answer",
            blocks = new[] { new { type = blockType, text = new { type = textType, text = new string('a', length) } } }
        });
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content);

        if (valid)
        {
            await test.RunAsync();
            using var posted = JsonDocument.Parse(Assert.Single(test.Transport.Bodies));
            Assert.Equal(length, posted.RootElement.GetProperty("blocks")[0].GetProperty("text").GetProperty("text").GetString()!.Length);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.RunAsync());
            Assert.Empty(test.Transport.Bodies);
        }
    }

    [Fact]
    public async Task PreservesSupportedImageContextAndFieldBlocks()
    {
        const string content = """
            {
              "text":"Image and details",
              "blocks":[
                {"type":"image","image_url":"https://example.com/image.png","alt_text":"Example","title":{"type":"plain_text","text":"Picture"}},
                {"type":"context","elements":[{"type":"mrkdwn","text":"*Details*"},{"type":"image","image_url":"https://example.com/icon.png","alt_text":"Icon"}]},
                {"type":"section","fields":[{"type":"plain_text","text":"Field value"}]}
              ]
            }
            """;
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content);

        await test.RunAsync();

        using var posted = JsonDocument.Parse(Assert.Single(test.Transport.Bodies));
        var blocks = posted.RootElement.GetProperty("blocks");
        Assert.Equal(3, blocks.GetArrayLength());
        Assert.Equal("https://example.com/image.png", blocks[0].GetProperty("image_url").GetString());
        Assert.Equal(2, blocks[1].GetProperty("elements").GetArrayLength());
        Assert.Equal("Field value", blocks[2].GetProperty("fields")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("""{"type":"image","image_url":"file:///local.png","alt_text":"Local file"}""")]
    [InlineData("""{"type":"image","image_url":"https://example.com/image.png"}""")]
    [InlineData("""{"type":"context","elements":[]}""")]
    [InlineData("""{"type":"section","fields":[]}""")]
    [InlineData("""{"type":"section","text":{"type":"mrkdwn","text":"Answer"},"accessory":{"type":"button"}}""")]
    public async Task RejectsUnsupportedOrIncompleteBlockContent(string block)
    {
        string content = """{"text":"Answer","blocks":[""" + block + "]}";
        using var test = new Harness(new CopilotStudioResponse("Answer", []), content);

        await Assert.ThrowsAsync<InvalidOperationException>(() => test.RunAsync());

        Assert.Empty(test.Transport.Bodies);
    }

    private sealed class Harness : IDisposable
    {
        public Mock<IChatClient> ChatClient { get; } = new();
        public List<ChatMessage[]> ModelInputs { get; } = [];
        public List<ChatOptions?> Options { get; } = [];
        public RecordingTransport Transport { get; } = new();
        public Mock<ISlackTurnContext> Context { get; } = new(MockBehavior.Strict);
        public TempState Temp { get; } = new();
        public ConversationState Conversation { get; } = new(new MemoryStorage());
        public TestBridge Agent { get; }
        private readonly Mock<ITurnState> _state = new();
        private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
        private bool _loaded;

        public Harness(CopilotStudioResponse response, string content, string? threadTs = "123.456")
        {
            ChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken _) =>
                {
                    ModelInputs.Add(messages.ToArray());
                    Options.Add(options);
                    return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, content)));
                });
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("SlackApi")).Returns(() => new HttpClient(Transport, disposeHandler: false));
            var slackEvent = new Dictionary<string, object> { ["channel"] = "C-context", ["type"] = "message" };
            if (threadTs != null)
            {
                slackEvent["thread_ts"] = threadTs;
            }
            var envelope = ProtocolJsonSerializer.ToObject<EventEnvelope>(
                JsonSerializer.Serialize(new Dictionary<string, object> { ["event"] = slackEvent }));
            var activity = new SlackActivity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Slack,
                Conversation = new ConversationAccount { Id = "conversation-1" },
                ChannelData = new SlackChannelData
                {
                    Envelope = envelope,
                    ApiToken = "test-token"
                }
            };
            Assert.Equal("C-context", activity.ChannelData.Channel);
            Assert.Equal(threadTs, activity.ChannelData.ThreadTs);
            var slackApi = new SlackApi(factory.Object);
            var services = new TurnContextStateCollection();
            services.Set(slackApi);
            Context.SetupGet(c => c.Activity).Returns(activity);
            Context.As<ITurnContext>().SetupGet(c => c.Activity).Returns(activity);
            Context.As<ITurnContext>().SetupGet(c => c.StackState).Returns(new TurnContextStateCollection());
            Context.As<ITurnContext>().SetupGet(c => c.Services).Returns(services);
            Context.SetupGet(c => c.Client).Returns(slackApi);
            _state.SetupGet(s => s.Temp).Returns(Temp);
            _state.SetupGet(s => s.Conversation).Returns(Conversation);
            Agent = new TestBridge(response, ChatClient.Object, _services);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (!_loaded)
            {
                await Conversation.LoadAsync(Context.Object, cancellationToken: cancellationToken);
                _loaded = true;
            }
            await Agent.OnSlackMessageAsync(Context.Object, _state.Object, cancellationToken);
        }

        public void Dispose()
        {
            Transport.Dispose();
            _services.Dispose();
        }
    }

    private sealed class TestBridge(CopilotStudioResponse response, IChatClient chatClient, IServiceProvider services)
        : McsSlackBridge(
            new AgentApplicationOptions(new MemoryStorage(), NullLoggerFactory.Instance),
            chatClient, new ConfigurationBuilder().Build(), services, NullLogger<McsSlackBridge>.Instance)
    {
        public int ProcessingCalls { get; private set; }

        protected override Task<CopilotStudioResponse> ProcessCopilotStudioResponseAsync(
            ITurnContext context, ITurnState turnState, object additionalData, CancellationToken cancellationToken)
        {
            ProcessingCalls++;
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingTransport : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public List<string> AuthorizationHeaders { get; } = [];
        public List<string> Urls { get; } = [];
        public string Response { get; set; } = """{"ok":true,"ts":"987.654"}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            bool isPostedMessage = url.EndsWith("/chat.postMessage", StringComparison.Ordinal);
            if (isPostedMessage)
            {
                Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                AuthorizationHeaders.Add(request.Headers.Authorization!.ToString());
                Urls.Add(url);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    isPostedMessage ? Response : """{"ok":true,"ts":"987.654"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}

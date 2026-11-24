// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Teams.Samples.BotAttachments;
using Microsoft.Teams.Samples.BotAttachments.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "bot-attachments")]
    public async Task AttachmentGreeting_UsesAgentTerminologyAsync()
    {
        await using var host = CreateHost(services => new BotAttachmentsAgent(
            CreateOptions(services), services.GetRequiredService<IHttpClientFactory>(),
            new PendingUploadStore(), new CapturingUploadQueue(),
            NullLogger<BotAttachmentsAgent>.Instance));

        await host.CreateTestFlow()
            .Send("hello")
            .AssertReply(activity => Assert.Equal(
                "Welcome to the Agent Attachments sample! Please attach a file or image to save to your OneDrive!",
                activity.Text))
            .AssertNoMoreReplies()
            .StartTestAsync();
    }

    [Fact]
    [Trait("Sample", "bot-attachments")]
    public async Task FileConsentDecline_ConfirmsTheFileWillNotBeUploadedAsync()
    {
        await using var host = CreateHost(services =>
        {
            var http = services.GetRequiredService<IHttpClientFactory>();
            return new BotAttachmentsAgent(CreateOptions(services), http, new PendingUploadStore(),
                new FileUploadQueue(http, new TestAdapter(), NullLogger<FileUploadQueue>.Instance),
                NullLogger<BotAttachmentsAgent>.Instance);
        });
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "fileConsent/invoke",
                Value = new { action = "decline", context = new { filename = "contract.txt", file_id = "file-1" } } })
            .AssertReply(activity =>
            {
                Assert.Contains("contract.txt", activity.Text);
                Assert.Contains("won't upload", activity.Text);
            })
            .AssertReply(activity =>
            {
                Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
                Assert.Equal(200, Payload(activity.Value)["status"]!.Value<int>());
            }).AssertNoMoreReplies().StartTestAsync();
    }
    [Fact]
    [Trait("Sample", "bot-attachments")]
    public async Task FileConsentAccept_QueuesOriginalBytesAndUploadMetadataAsync()
    {
        var pending = new PendingUploadStore();
        byte[] bytes = [1, 2, 3];
        pending.Add("file-1", bytes);
        var queue = new CapturingUploadQueue();
        await using var host = CreateHost(services => new BotAttachmentsAgent(
            CreateOptions(services), services.GetRequiredService<IHttpClientFactory>(), pending,
            queue, NullLogger<BotAttachmentsAgent>.Instance));

        await host.CreateTestFlow()
            .Send(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "fileConsent/invoke",
                Value = new
                {
                    action = "accept",
                    context = new { filename = "contract.txt", file_id = "file-1" },
                    uploadInfo = new
                    {
                        name = "contract.txt", uploadUrl = "https://upload.test/file",
                        contentUrl = "https://content.test/file", uniqueId = "file-1", fileType = "txt"
                    }
                }
            })
            .AssertReplyContains("Accepted. Uploading")
            .AssertReply(activity => Assert.Equal(200, Payload(activity.Value)["status"]!.Value<int>()))
            .AssertNoMoreReplies().StartTestAsync();

        Assert.NotNull(queue.Item);
        Assert.Equal(bytes, queue.Item.Content);
        Assert.Equal("https://upload.test/file", queue.Item.UploadUrl);
        Assert.Equal("https://content.test/file", queue.Item.ContentUrl);
        Assert.Equal("contract.txt", queue.Item.FileName);
        Assert.False(pending.TryTake("file-1", out _));
    }

    [Fact]
    [Trait("Sample", "bot-attachments")]
    public async Task FileDownload_PreservesConsentCardContextAndSizeAsync()
    {
        byte[] bytes = [1, 2, 3];
        var clients = new ContractHttpClientFactory(request =>
        {
            Assert.Equal("https://download.test/file", request.RequestUri!.ToString());
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        });
        var pending = new PendingUploadStore();
        await using var host = CreateHost(services => new BotAttachmentsAgent(
            CreateOptions(services), services.GetRequiredService<IHttpClientFactory>(), pending,
            new CapturingUploadQueue(), NullLogger<BotAttachmentsAgent>.Instance), clients);

        await host.CreateTestFlow()
            .Send(new Activity
            {
                Type = ActivityTypes.Message,
                Attachments = [new Attachment
                {
                    ContentType = "application/vnd.microsoft.teams.file.download.info",
                    Name = "contract.txt",
                    Content = new { downloadUrl = "https://download.test/file" }
                }]
            })
            .AssertReplyContains("Received")
            .AssertReply(activity =>
            {
                var attachment = Assert.Single(activity.Attachments);
                Assert.Equal("application/vnd.microsoft.teams.card.file.consent", attachment.ContentType);
                var card = Payload(attachment.Content);
                Assert.Equal(bytes.Length, card["sizeInBytes"]!.Value<int>());
                Assert.Equal("contract.txt", card["acceptContext"]!["filename"]!.Value<string>());
                Assert.True(JToken.DeepEquals(card["acceptContext"], card["declineContext"]));
                Assert.True(pending.TryTake(card["acceptContext"]!["file_id"]!.Value<string>()!, out var stored));
                Assert.Equal(bytes, stored);
            }).AssertNoMoreReplies().StartTestAsync();
    }

    private sealed class CapturingUploadQueue : IFileUploadQueue
    {
        public FileUploadWorkItem? Item { get; private set; }

        public bool TryQueue(FileUploadWorkItem workItem)
        {
            Item = workItem;
            return true;
        }
    }

}

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
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using BotMessageExtensions;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "bot-message-extensions")]
    public async Task QueryLink_ReturnsCardPreviewContainingRequestedUrlAsync()
    {
        await using var host = CreateHost(services => new BotMessageExtensionsAgent(
            CreateOptions(services), services.GetRequiredService<IHttpClientFactory>()));
        await host.CreateTestFlow()
            .Send(new Activity { Type = ActivityTypes.Invoke, Name = "composeExtension/queryLink",
                Value = new { url = "https://example.test/article" } })
            .AssertReply(activity =>
            {
                Assert.Equal(ActivityTypes.InvokeResponse, activity.Type);
                var response = Payload(activity.Value);
                Assert.Equal(200, response["status"]!.Value<int>());
                var extension = response["body"]!["composeExtension"]!;
                Assert.Equal("result", extension["type"]!.Value<string>());
                var attachment = Assert.Single(extension["attachments"]!.Children());
                Assert.Equal(ContentTypes.AdaptiveCard, attachment["contentType"]!.Value<string>());
                Assert.Contains(attachment["content"]!["body"]!.Children(), element =>
                    element["text"]?.Value<string>()?.Contains("https://example.test/article") == true);
            }).AssertNoMoreReplies().StartTestAsync();
    }
}

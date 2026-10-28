// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using BotMeetings;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeamsSampleSync.ContractTests;

public partial class TeamsSampleContracts
{
    [Fact]
    [Trait("Sample", "bot-meetings")]
    public async Task MeetingStart_ReturnsMeetingTitleAndJoinLinkAsync()
    {
        await using var host = CreateHost(services => new BotMeetingsAgent(
            CreateOptions(services), new ConfigurationBuilder().Build()));
        await host.CreateTestFlow()
            .Send(new Activity
            {
                Type = ActivityTypes.Event, Name = "application/vnd.microsoft.meetingStart",
                Value = new { id = "meeting-1", title = "Contract meeting", type = "Scheduled", msGraphResourceId = "graph-meeting-1",
                    joinUrl = "https://teams.microsoft.com/l/meetup-join/contract",
                    startTime = "2030-01-01T10:00:00Z" }
            })
            .AssertReply(activity =>
            {
                var attachment = Assert.Single(activity.Attachments);
                Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
                var card = Payload(attachment.Content);
                Assert.Contains(card["body"]!.Children(), element =>
                    element["text"]?.Value<string>()?.Contains("Contract meeting") == true);
                Assert.Equal("https://teams.microsoft.com/l/meetup-join/contract",
                    card["actions"]!.Single()["url"]!.Value<string>());
            })
            .AssertNoMoreReplies().StartTestAsync();
    }
}

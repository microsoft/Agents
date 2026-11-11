// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Connector;
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
    public async Task MeetingMetadata_RetainsGraphIdentifiersThroughAuthenticatedTransportAsync()
    {
        var clients = new ContractHttpClientFactory(request =>
        {
            Assert.Equal("/region/v1/meetings/meeting%2Fid", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"organizer":{"aadObjectId":"organizer-1"},"details":{"msGraphResourceId":"graph-meeting-1"}}""")
            };
        });
        using var connector = new RestConnectorClient(
            new Uri("https://connector.invalid/region/"), clients, () => Task.FromResult("test-token"));
        using var context = new TurnContext(
            new TestAdapter(), new Activity(), new ClaimsIdentity());
        context.Services.Set<IConnectorClient>(connector);

        var metadata = await MeetingMetadata.LoadAsync(context, "meeting/id", default);

        Assert.Equal("organizer-1", metadata.OrganizerId);
        Assert.Equal("graph-meeting-1", metadata.GraphMeetingId);
    }

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
                Assert.Contains(card["body"]!.Children(), element =>
                    element["text"]?.Value<string>()?.Contains("2030-01-01T10:00:00Z") == true);
                Assert.Equal("https://teams.microsoft.com/l/meetup-join/contract",
                    card["actions"]!.Single()["url"]!.Value<string>());
            })
            .AssertNoMoreReplies().StartTestAsync();
    }
    [Fact]
    [Trait("Sample", "bot-meetings")]
    public async Task MeetingEnd_PreservesTimestampAndHandlesMissingTranscriptMetadataAsync()
    {
        var requests = 0;
        var clients = new ContractHttpClientFactory(request =>
        {
            requests++;
            Assert.Equal("/v1/meetings/meeting-1", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"organizer":{},"details":{}}""")
            };
        });
        await using var host = CreateHost(services => new BotMeetingsAgent(
            CreateOptions(services), new ConfigurationBuilder().Build()), teamsHttpClients: clients);
        await host.CreateTestFlow()
            .Send(new Activity
            {
                Type = ActivityTypes.Event, Name = "application/vnd.microsoft.meetingEnd",
                Value = new { id = "meeting-1", title = "Contract meeting", endTime = "2030-01-01T11:00:00Z" }
            })
            .AssertReply(activity =>
            {
                var card = Payload(Assert.Single(activity.Attachments).Content);
                Assert.Contains(card["body"]!.Children(), element =>
                    element["text"]?.Value<string>()?.Contains("2030-01-01T11:00:00Z") == true);
                Assert.Contains(card["body"]!.Children(), element =>
                    element["text"]?.Value<string>() == "Transcript not available for this meeting.");
            }).AssertNoMoreReplies().StartTestAsync();
        Assert.Equal(1, requests);
    }

}

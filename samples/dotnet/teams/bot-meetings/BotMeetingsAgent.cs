// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.Meetings;
using Microsoft.Agents.Extensions.MSTeams.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Teams.Api.Meetings;
using Microsoft.Teams.Cards;

namespace BotMeetings;

[TeamsExtension]
public partial class BotMeetingsAgent : AgentApplication
{
    private readonly GraphServiceClient? _graphClient;

    public BotMeetingsAgent(AgentApplicationOptions options, IConfiguration configuration)
        : base(options)
    {
        _graphClient = CreateGraphClient(configuration.GetSection("Graph"));
    }

    [TeamsMeetingParticipantsJoinRoute]
    public async Task OnMeetingParticipantsJoinAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MeetingParticipantsEventDetails participants,
        CancellationToken cancellationToken)
    {
        var participant = participants.Members.FirstOrDefault();
        if (string.IsNullOrEmpty(participant?.User?.AadObjectId))
        {
            return;
        }

        string member = participant.User.Name ?? string.Empty;
        string role = participant.Meeting?.Role ?? "a participant";
        var card = new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Body =
            [
                new TextBlock($"{member} has joined the meeting as {role}.")
                {
                    Wrap = true,
                    Weight = TextWeight.Bolder
                }
            ]
        };

        await SendCardAsync(turnContext, card, cancellationToken);
    }

    [TeamsMeetingStartRoute]
    public async Task OnMeetingStartAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MeetingDetails meeting,
        CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Body =
            [
                new TextBlock("The meeting has started.")
                {
                    Wrap = true,
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Large
                },
                new TextBlock($"**Title:** {meeting.Title}")
                {
                    Wrap = true
                },
                new TextBlock($"**Start Time:** {meeting.ScheduledStartTime}")
                {
                    Wrap = true
                }
            ],
            Actions =
            [
                new OpenUrlAction(meeting.JoinUrl)
                {
                    Title = "Join Meeting"
                }
            ]
        };

        await SendCardAsync(turnContext, card, cancellationToken);
    }

    [TeamsMeetingEndRoute]
    public async Task OnMeetingEndAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MeetingDetails meeting,
        CancellationToken cancellationToken)
    {
        var meetingInfo = await turnContext.Client.Meetings.GetByIdAsync(meeting.Id);
        string userId = meetingInfo?.Organizer?.AadObjectId ?? string.Empty;
        string? graphMeetingId = meetingInfo?.Details?.MSGraphResourceId;

        string transcript = string.Empty;
        if (_graphClient is not null &&
            !string.IsNullOrEmpty(graphMeetingId) &&
            !string.IsNullOrEmpty(userId))
        {
            string vttTranscript = await GetMeetingTranscriptAsync(
                graphMeetingId,
                userId,
                cancellationToken);
            if (!string.IsNullOrEmpty(vttTranscript))
            {
                transcript = ParseVtt(vttTranscript);
            }
        }

        var cardBody = new List<CardElement>
        {
            new TextBlock("The meeting has ended.")
            {
                Wrap = true,
                Weight = TextWeight.Bolder,
                Size = TextSize.Large
            },
            new TextBlock($"**End Time:** {meeting.ScheduledEndTime}")
            {
                Wrap = true
            },
            new TextBlock("**Transcript:**")
            {
                Wrap = true,
                Weight = TextWeight.Bolder
            }
        };

        if (!string.IsNullOrEmpty(transcript))
        {
            foreach (string line in transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    cardBody.Add(new TextBlock(line) { Wrap = true });
                }
            }
        }
        else
        {
            cardBody.Add(new TextBlock("Transcript not available for this meeting.") { Wrap = true });
        }

        await SendCardAsync(
            turnContext,
            new AdaptiveCard
            {
                Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                Body = cardBody
            },
            cancellationToken);
    }

    [TeamsMeetingParticipantsLeaveRoute]
    public async Task OnMeetingParticipantsLeaveAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MeetingParticipantsEventDetails participants,
        CancellationToken cancellationToken)
    {
        string? member = participants.Members.FirstOrDefault()?.User?.Name;
        if (string.IsNullOrEmpty(member))
        {
            return;
        }

        var card = new AdaptiveCard
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Body =
            [
                new TextBlock($"{member} has left the meeting.")
                {
                    Wrap = true,
                    Weight = TextWeight.Bolder
                }
            ]
        };

        await SendCardAsync(turnContext, card, cancellationToken);
    }

    private async Task<string> GetMeetingTranscriptAsync(
        string meetingResourceId,
        string userId,
        CancellationToken cancellationToken)
    {
        var transcriptsMetadata = await _graphClient!.Users[userId]
            .OnlineMeetings[meetingResourceId]
            .Transcripts
            .GetAsync(cancellationToken: cancellationToken);

        if (transcriptsMetadata?.Value is not { Count: > 0 })
        {
            return string.Empty;
        }

        string? transcriptId = transcriptsMetadata.Value
            .OrderByDescending(transcript => transcript.CreatedDateTime)
            .FirstOrDefault()
            ?.Id;
        if (transcriptId is null)
        {
            return string.Empty;
        }

        Stream? content = await _graphClient.Users[userId]
            .OnlineMeetings[meetingResourceId]
            .Transcripts[transcriptId]
            .Content
            .GetAsync(
                requestConfiguration: config => config.Headers.Add("Accept", "text/vtt"),
                cancellationToken: cancellationToken);
        if (content is null)
        {
            return string.Empty;
        }

        await using (content)
        using (var reader = new StreamReader(content))
        {
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }

    private static string ParseVtt(string vtt)
    {
        var lines = new List<string>();
        foreach (string line in vtt.Split('\n'))
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) ||
                trimmedLine.StartsWith("WEBVTT", StringComparison.Ordinal) ||
                trimmedLine.Contains("-->", StringComparison.Ordinal))
            {
                continue;
            }

            string processedLine = Regex.Replace(trimmedLine, @"<v ([^>]+)>", "$1: ");
            processedLine = Regex.Replace(processedLine, @"<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrEmpty(processedLine))
            {
                lines.Add(processedLine);
            }
        }

        return string.Join("\n", lines);
    }

    private static Task SendCardAsync(
        ITeamsTurnContext turnContext,
        AdaptiveCard card,
        CancellationToken cancellationToken)
        => turnContext.SendActivityAsync(
            MessageFactory.Attachment(new Attachment
            {
                ContentType = ContentTypes.AdaptiveCard,
                Content = card
            }),
            cancellationToken);

    private static GraphServiceClient? CreateGraphClient(IConfiguration graphConfiguration)
    {
        string tenantId = graphConfiguration["TenantId"] ?? string.Empty;
        string clientId = graphConfiguration["ClientId"] ?? string.Empty;
        string clientSecret = graphConfiguration["ClientSecret"] ?? string.Empty;

        if (!IsConfigured(tenantId) || !IsConfigured(clientId) || !IsConfigured(clientSecret))
        {
            return null;
        }

        try
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsConfigured(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           !value.Contains("{{", StringComparison.Ordinal) &&
           !value.StartsWith("<", StringComparison.Ordinal);
}

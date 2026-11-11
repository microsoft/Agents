// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.MessageExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Cards;
using MsgExt = Microsoft.Teams.Apps.MessageExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AdaptiveCard = Microsoft.Teams.Cards.AdaptiveCard;

namespace BotMessageExtensions;

[TeamsExtension]
public partial class BotMessageExtensionsAgent(
    AgentApplicationOptions options,
    IHttpClientFactory httpClientFactory) : AgentApplication(options)
{
    [TeamsQueryRoute("wikipediaSearch")]
    public async Task<MsgExt.MessageExtensionResponse> OnWikipediaSearchAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MsgExt.MessageExtensionQuery query,
        CancellationToken cancellationToken)
    {
        string searchQuery = query.Parameters?.FirstOrDefault()?.Value?.ToString() ?? string.Empty;

        Logger.LogInformation("Query: command=wikipediaSearch, query={SearchQuery}", searchQuery);

        List<TeamsAttachment> attachments = (await SearchWikipediaAsync(searchQuery, cancellationToken))
            .Select(result =>
            {
                string title = result["title"]?.ToString() ?? "No Title";
                string snippet = Regex.Replace(result["snippet"]?.ToString() ?? string.Empty, "<[^>]+>", string.Empty);
                return CreateAttachment(CreateWikipediaCard(result), title, snippet);
            })
            .ToList();

        if (attachments.Count == 0)
        {
            return new MsgExt.MessageExtensionResponse
            {
                ComposeExtension = new MsgExt.ComposeExtension
                {
                    Type = MsgExt.MessageExtensionResponseTypes.Message,
                    Text = $"No results found for '{searchQuery}'"
                }
            };
        }

        return new MsgExt.MessageExtensionResponse
        {
            ComposeExtension = new MsgExt.ComposeExtension
            {
                Type = MsgExt.MessageExtensionResponseTypes.Result,
                AttachmentLayout = TeamsAttachmentLayouts.List,
                Attachments = attachments
            }
        };
    }

    [TeamsQueryLinkRoute]
    public Task<MsgExt.MessageExtensionResponse> OnQueryLinkAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        MsgExt.MessageExtensionQueryLink? query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(CardResultResponse(CreateLinkPreviewCard(query?.Url?.ToString() ?? string.Empty)));
    }

    [TeamsMessageRoute(textRegex: "(?i)help", rank: 1)]
    public async Task OnHelpAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(
            "Hi! I'm the Search Messaging Extension Agent!\n\n" +
            "Use me in the compose area to search for Wikipedia articles\n",
            cancellationToken: cancellationToken);
    }

    [TeamsMessageRoute]
    public async Task OnMessageAsync(
        ITeamsTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(
            $"You said: {turnContext.Activity.Text}\n\nType 'help' to learn more.",
            cancellationToken: cancellationToken);
    }

    private static AdaptiveCard CreateWikipediaCard(JToken result)
    {
        string title = result["title"]?.ToString() ?? "No Title";
        string snippet = Regex.Replace(result["snippet"]?.ToString() ?? string.Empty, "<[^>]+>", string.Empty);

        return new AdaptiveCard
        {
            Version = Microsoft.Teams.Cards.Version.Version1_4,
            Body =
            [
                new TextBlock(title) { Weight = TextWeight.Bolder, Size = TextSize.Large },
                new TextBlock(snippet) { Wrap = true, IsSubtle = true }
            ],
            Actions =
            [
                new OpenUrlAction($"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}") { Title = "Read on Wikipedia" }
            ]
        };
    }

    private static AdaptiveCard CreateLinkPreviewCard(string url)
    {
        return new AdaptiveCard
        {
            Version = Microsoft.Teams.Cards.Version.Version1_4,
            Body =
            [
                new TextBlock("Link Preview") { Weight = TextWeight.Bolder, Size = TextSize.Medium },
                new TextBlock($"URL: {url}") { IsSubtle = true, Wrap = true },
                new TextBlock("This is a preview of the linked content generated by the message extension.")
                {
                    Wrap = true,
                    Size = TextSize.Small
                }
            ]
        };
    }

    private async Task<List<JToken>> SearchWikipediaAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            HttpClient httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BotMessageExtensions/1.0 (Teams Bot; +https://example.com)");

            Dictionary<string, string> parameters = new()
            {
                ["action"] = "query",
                ["format"] = "json",
                ["list"] = "search",
                ["srsearch"] = query,
                ["utf8"] = "1",
                ["srlimit"] = "8"
            };

            string queryString = string.Join(
                "&",
                parameters.Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"));
            string url = $"https://en.wikipedia.org/w/api.php?{queryString}";

            string response = await httpClient.GetStringAsync(url, cancellationToken);
            JObject json = JObject.Parse(response);
            JArray? searchResults = json["query"]?["search"] as JArray;

            return searchResults?.ToList() ?? [];
        }
        catch (HttpRequestException exception)
        {
            Logger.LogError(exception, "Wikipedia search failed");
            return [];
        }
        catch (JsonException exception)
        {
            Logger.LogError(exception, "Wikipedia search response could not be parsed");
            return [];
        }
    }

    private static TeamsAttachment CreateAttachment(AdaptiveCard card, string title, string text)
    {
        return new TeamsAttachment
        {
            ContentType = AttachmentContentTypes.AdaptiveCard,
            Content = card,
            Properties = new()
            {
                ["preview"] = new TeamsAttachment
                {
                    ContentType = AttachmentContentTypes.ThumbnailCard,
                    Content = new ThumbnailCard { Title = title, Text = text }
                }
            }
        };
    }

    private static MsgExt.MessageExtensionResponse CardResultResponse(AdaptiveCard card)
    {
        return new MsgExt.MessageExtensionResponse
        {
            ComposeExtension = new MsgExt.ComposeExtension
            {
                Type = MsgExt.MessageExtensionResponseTypes.Result,
                AttachmentLayout = TeamsAttachmentLayouts.List,
                Attachments =
                [
                    new TeamsAttachment
                    {
                        ContentType = AttachmentContentTypes.AdaptiveCard,
                        Content = card
                    }
                ]
            }
        };
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace RetrievalAgent.Services;

public static class BuildGenieResponses
{
    public static string For(RetrievalStatus status) => status switch
    {
        RetrievalStatus.NotSignedIn => "Please sign in to Microsoft 365, then ask your Build question again.",
        RetrievalStatus.NoResults => "I couldn't find Build session information in the configured SharePoint site. Check the site URL, document permissions, and indexing, then try a more specific question.",
        _ => "I couldn't retrieve Build session information right now. Please try again later.",
    };

    public static string GroundedAnswer(IReadOnlyList<RetrievalItem> items) =>
        "Here is what I found in the configured SharePoint site:\n\n" + string.Join("\n\n", items.Select(item => $"{item.Extract}\nSource: {item.SourceUrl}"));
}

public static class BuildGenieSourceCard
{
    public static IActivity Create(IReadOnlyList<RetrievalItem> items)
    {
        List<object> body = [];
        foreach (RetrievalItem item in items)
        {
            body.Add(new
            {
                type = "Container",
                separator = true,
                items = new object[]
                {
                    new { type = "TextBlock", text = item.Title, weight = "Bolder", wrap = true },
                    new { type = "TextBlock", text = item.Extract, wrap = true },
                },
                selectAction = new { type = "Action.OpenUrl", title = "Open source", url = item.SourceUrl },
            });
        }

        return MessageFactory.Attachment(new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = new { type = "AdaptiveCard", version = "1.5", body },
        });
    }
}

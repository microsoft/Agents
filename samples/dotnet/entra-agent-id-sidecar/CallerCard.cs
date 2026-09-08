// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

namespace EntraAgentIdSidecar;

internal static class CallerCard
{
    public static Attachment Create(ITurnContext context)
    {
        ClaimsIdentity? identity = context.Identity;
        ChannelAccount? recipient = context.Activity.Recipient;

        var facts = new List<object>
        {
            Fact("Claims available", identity?.Claims.Any() == true ? "Yes" : "No"),
            Fact("Authenticated", identity?.IsAuthenticated == true ? "Yes" : "No"),
            Fact("Authentication type", identity?.AuthenticationType),
            Fact("Issuer", Claim(identity, "iss")),
            Fact("Audience", Claim(identity, "aud")),
            Fact("Tenant ID", Claim(identity, "tid", "http://schemas.microsoft.com/identity/claims/tenantid")),
            Fact("Subject", Claim(identity, "sub", ClaimTypes.NameIdentifier)),
            Fact("Caller app ID", Claim(identity, "azp", "appid")),
            Fact("Token version", Claim(identity, "ver")),
            Fact("Issued", EpochClaim(identity, "iat")),
            Fact("Expires", EpochClaim(identity, "exp")),
            Fact("Recipient role", recipient?.Role),
            Fact("Agent identity", recipient?.AgenticAppId),
            Fact("Agent user", recipient?.AgenticUserId),
            Fact("Activity tenant", recipient?.TenantId),
        };

        return new Attachment
        {
            ContentType = ContentTypes.AdaptiveCard,
            Content = new
            {
                type = "AdaptiveCard",
                version = "1.5",
                body = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        size = "Medium",
                        weight = "Bolder",
                        text = "Inbound agentic caller",
                    },
                    new
                    {
                        type = "TextBlock",
                        wrap = true,
                        text = "Selected token claims and activity routing values. The raw token is never displayed.",
                    },
                    new
                    {
                        type = "FactSet",
                        facts,
                    },
                },
            },
        };
    }

    private static object Fact(string title, string? value) =>
        new { title, value = string.IsNullOrWhiteSpace(value) ? "Unavailable" : value };

    private static string? Claim(ClaimsIdentity? identity, params string[] types) =>
        identity?.Claims.FirstOrDefault(claim => types.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))?.Value;

    private static string? EpochClaim(ClaimsIdentity? identity, string type)
    {
        string? value = Claim(identity, type);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.ToString("u", CultureInfo.InvariantCulture)
            : value;
    }
}

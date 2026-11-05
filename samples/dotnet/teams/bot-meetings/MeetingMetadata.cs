// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;

namespace BotMeetings;

public sealed record MeetingMetadata(string OrganizerId, string GraphMeetingId)
{
    // The SDK 1.9 Teams meeting model omits msGraphResourceId. Read the wire
    // response through the turn's authenticated transport to preserve transcripts.
    public static async Task<MeetingMetadata> LoadAsync(
        ITurnContext context, string meetingId, CancellationToken cancellationToken)
    {
        var transport = (IRestTransport)context.Services.Get<IConnectorClient>();
        using var client = await transport.GetHttpClientAsync();
        var endpoint = new Uri($"{transport.Endpoint.ToString().TrimEnd('/')}/v1/meetings/{Uri.EscapeDataString(meetingId)}");
        using var response = await client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return new MeetingMetadata(
            ReadString(json.RootElement, "organizer", "aadObjectId"),
            ReadString(json.RootElement, "details", "msGraphResourceId"));
    }

    private static string ReadString(JsonElement root, string section, string name)
    {
        if (root.TryGetProperty(section, out var value) && value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public static string EventTime(object value, string name)
    {
        // MeetingDetails also omits event timestamps; retain them from the activity.
        var payload = JsonSerializer.SerializeToElement(value);

        foreach (var property in payload.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ToString();
            }
        }

        return string.Empty;
    }
}

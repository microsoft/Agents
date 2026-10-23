using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Slack_MCS_Bridge;

internal static class SlackMessageContent
{
    public static Dictionary<string, object> CreatePayload(string content, string channel, string? threadTs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        string text = RequiredString(root, "text", 40000);
        JsonElement blocks = RequiredArray(root, "blocks", 50);
        foreach (JsonElement block in blocks.EnumerateArray())
        {
            ValidateBlock(block);
        }

        // Only content is model-controlled; never forward model-supplied routing or identity fields.
        var message = new Dictionary<string, object>
        {
            ["channel"] = channel,
            ["text"] = text,
            ["blocks"] = blocks.Clone()
        };
        if (!string.IsNullOrWhiteSpace(threadTs))
        {
            message["thread_ts"] = threadTs;
        }
        return message;
    }

    private static void ValidateBlock(JsonElement block)
    {
        string type = RequiredString(block, "type", 50);
        if (block.TryGetProperty("block_id", out _))
        {
            RequiredString(block, "block_id", 255);
        }
        switch (type)
        {
            case "section":
                bool hasText = block.TryGetProperty("text", out JsonElement text);
                bool hasFields = block.TryGetProperty("fields", out _);
                if (!hasText && !hasFields)
                {
                    throw new InvalidOperationException("Slack section requires text or fields.");
                }
                if (hasText)
                {
                    ValidateText(text, 3000);
                }
                if (hasFields)
                {
                    foreach (JsonElement field in RequiredArray(block, "fields", 10).EnumerateArray())
                    {
                        ValidateText(field, 2000);
                    }
                }
                if (block.TryGetProperty("accessory", out _))
                {
                    throw new InvalidOperationException("Slack section accessories are not supported by this formatter.");
                }
                break;
            case "header":
                ValidateText(block.GetProperty("text"), 150, plainTextOnly: true);
                break;
            case "divider":
                break;
            case "image":
                ValidateImage(block);
                if (block.TryGetProperty("title", out JsonElement title))
                {
                    ValidateText(title, 2000, plainTextOnly: true);
                }
                break;
            case "context":
                foreach (JsonElement element in RequiredArray(block, "elements", 10).EnumerateArray())
                {
                    if (RequiredString(element, "type", 50) == "image")
                    {
                        ValidateImage(element);
                    }
                    else
                    {
                        ValidateText(element, 3000);
                    }
                }
                break;
            default:
                throw new InvalidOperationException("The formatter returned an unsupported Slack block type.");
        }
    }

    private static void ValidateText(JsonElement value, int maxLength, bool plainTextOnly = false)
    {
        string type = RequiredString(value, "type", 50);
        if (type != "plain_text" && (plainTextOnly || type != "mrkdwn"))
        {
            throw new InvalidOperationException("The formatter returned an invalid Slack text object.");
        }
        RequiredString(value, "text", maxLength);
    }

    private static void ValidateImage(JsonElement value)
    {
        string url = RequiredString(value, "image_url", 3000);
        RequiredString(value, "alt_text", 2000);
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            throw new InvalidOperationException("Slack image URLs must use HTTP or HTTPS.");
        }
    }

    private static string RequiredString(JsonElement parent, string name, int maxLength)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
            || value.GetString()!.Length > maxLength)
        {
            throw new InvalidOperationException($"Slack content requires a nonempty '{name}' string of at most {maxLength} characters.");
        }
        return value.GetString()!;
    }

    private static JsonElement RequiredArray(JsonElement parent, string name, int maxCount)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() == 0
            || value.GetArrayLength() > maxCount)
        {
            throw new InvalidOperationException($"Slack content requires a '{name}' array with 1 to {maxCount} items.");
        }
        return value;
    }
}

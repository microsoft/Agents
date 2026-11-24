// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace ProductionReference;

public sealed record IssueCaptureState(
    int Version = 1,
    string Stage = "summary",
    string? Summary = null,
    string? Impact = null);

public static class IssueCapture
{
    public static (IssueCaptureState State, string Reply) Advance(IssueCaptureState current, string message)
    {
        if (current.Version != 1)
        {
            throw new InvalidOperationException("Unsupported state schema.");
        }
        var value = message.Trim();
        if (value.Length == 0)
        {
            return (current, "Please provide the requested information.");
        }
        switch (current.Stage)
        {
            case "summary":
                return (current with { Summary = value, Stage = "impact" },
                    "What is the impact: low, medium, or high?");
            case "impact":
                var impact = value.ToLowerInvariant();
                return impact is "low" or "medium" or "high"
                    ? (current with { Impact = impact, Stage = "complete" },
                        $"Support issue capture saved to this conversation with {impact} impact.")
                    : (current, "Reply with low, medium, or high.");
            case "complete":
                return (current, "This conversation already has a completed support issue capture.");
            default:
                throw new InvalidOperationException("Unsupported state stage.");
        }
    }
}

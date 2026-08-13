// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

/// <summary>
/// Persisted state for an in-progress or completed support triage case.
/// Stored conversation-scoped via <see cref="SupportCaseStateAccessor"/>.
/// </summary>
public sealed class SupportCaseState
{
    /// <summary>Short description of the issue reported by the user.</summary>
    public string? IssueSummary { get; set; }

    /// <summary>Business impact of the issue as described by the user.</summary>
    public string? Impact { get; set; }

    /// <summary>Preferred contact channel (e.g. Email, Phone).</summary>
    public string? ContactPreference { get; set; }

    /// <summary>Timestamp of the most recent update to this state.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Returns true when all three required fields have been collected.</summary>
    public bool IsComplete =>
        !string.IsNullOrEmpty(IssueSummary) &&
        !string.IsNullOrEmpty(Impact) &&
        !string.IsNullOrEmpty(ContactPreference);
}
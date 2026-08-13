// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Xunit;

namespace ProductionReference.Tests;

public class SupportTriageDialogTests
{
    private static readonly SupportTriageDialog Dialog = new();

    [Fact]
    public void NextTurn_prompts_for_issue_summary_when_state_is_empty()
    {
        var state = new SupportCaseState();
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, null, now);

        Assert.False(result.IsComplete);
        Assert.Contains("issue", result.ResponseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NextTurn_captures_issue_summary_and_prompts_for_impact()
    {
        var state = new SupportCaseState();
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, "Login is broken", now);

        Assert.False(result.IsComplete);
        Assert.Equal("Login is broken", result.State.IssueSummary);
        Assert.Contains("impact", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        // Input state must not be mutated.
        Assert.Null(state.IssueSummary);
    }

    [Fact]
    public void NextTurn_captures_impact_and_prompts_for_contact_preference()
    {
        var state = new SupportCaseState { IssueSummary = "Login is broken" };
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, "High - production down", now);

        Assert.False(result.IsComplete);
        Assert.Equal("High - production down", result.State.Impact);
        Assert.Contains("contact", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        // Input state must not be mutated.
        Assert.Null(state.Impact);
    }

    [Fact]
    public void NextTurn_captures_contact_preference_and_returns_summary()
    {
        var state = new SupportCaseState
        {
            IssueSummary = "Login is broken",
            Impact = "High - production down",
        };
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, "Email", now);

        Assert.True(result.IsComplete);
        Assert.Equal("Email", result.State.ContactPreference);
        Assert.Contains("Login is broken", result.ResponseText);
        // Input state must not be mutated.
        Assert.Null(state.ContactPreference);
    }

    [Fact]
    public void NextTurn_keeps_completed_case_and_returns_summary_for_followup()
    {
        var state = new SupportCaseState
        {
            IssueSummary = "Login is broken",
            Impact = "High - production down",
            ContactPreference = "Email",
        };
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, "anything", now);

        Assert.True(result.IsComplete);
        Assert.Equal("Login is broken", result.State.IssueSummary);
        Assert.Contains("Login is broken", result.ResponseText);
    }

    [Fact]
    public void NextTurn_treats_whitespace_input_as_absent_and_reprompts_for_issue_summary()
    {
        var state = new SupportCaseState();
        var now = DateTimeOffset.UtcNow;

        SupportTriageTurnResult result = Dialog.NextTurn(state, "   ", now);

        Assert.False(result.IsComplete);
        Assert.Null(result.State.IssueSummary);
        Assert.Contains("issue", result.ResponseText, StringComparison.OrdinalIgnoreCase);
    }
}
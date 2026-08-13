// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

/// <summary>
/// Result returned by <see cref="SupportTriageDialog.NextTurn"/>.
/// </summary>
/// <param name="State">The updated support case state after processing the turn.</param>
/// <param name="ResponseText">The text to send back to the user.</param>
/// <param name="IsComplete">True when all required fields have been collected.</param>
public sealed record SupportTriageTurnResult(SupportCaseState State, string ResponseText, bool IsComplete);

/// <summary>
/// Pure, stateless dialog that drives the support triage flow.
/// Collects <see cref="SupportCaseState.IssueSummary"/>, <see cref="SupportCaseState.Impact"/>,
/// and <see cref="SupportCaseState.ContactPreference"/> one turn at a time.
/// Each call returns a <em>new</em> <see cref="SupportCaseState"/>; the input is never mutated.
/// </summary>
public sealed class SupportTriageDialog
{
    /// <summary>
    /// Advances the triage dialog by one turn.
    /// </summary>
    /// <param name="state">Current case state loaded from conversation storage.  Not mutated.</param>
    /// <param name="userText">Raw text from the user message; may be null or whitespace.</param>
    /// <param name="now">Current timestamp, stamped on any state change.</param>
    /// <returns>A <see cref="SupportTriageTurnResult"/> with a new state snapshot and reply text.</returns>
    public SupportTriageTurnResult NextTurn(SupportCaseState state, string? userText, DateTimeOffset now)
    {
        string? input = string.IsNullOrWhiteSpace(userText) ? null : userText.Trim();

        // Already complete — return the unchanged state and a summary for any follow-up message.
        if (state.IsComplete)
        {
            return new SupportTriageTurnResult(state, BuildSummary(state), true);
        }

        // Step 1: collect issue summary.
        if (string.IsNullOrEmpty(state.IssueSummary))
        {
            if (input is null)
            {
                return new SupportTriageTurnResult(
                    state,
                    "Please describe your issue. What problem are you experiencing?",
                    false);
            }

            var next = new SupportCaseState
            {
                IssueSummary = input,
                Impact = state.Impact,
                ContactPreference = state.ContactPreference,
                UpdatedAt = now,
            };
            return new SupportTriageTurnResult(
                next,
                "What is the impact of this issue? (e.g., High \u2013 production down, Medium \u2013 degraded, Low \u2013 minor inconvenience)",
                false);
        }

        // Step 2: collect impact.
        if (string.IsNullOrEmpty(state.Impact))
        {
            if (input is null)
            {
                return new SupportTriageTurnResult(
                    state,
                    "What is the impact of this issue?",
                    false);
            }

            var next = new SupportCaseState
            {
                IssueSummary = state.IssueSummary,
                Impact = input,
                ContactPreference = state.ContactPreference,
                UpdatedAt = now,
            };
            return new SupportTriageTurnResult(
                next,
                "How would you prefer to be contacted? (e.g., Email, Phone, Chat)",
                false);
        }

        // Step 3: collect contact preference.
        if (input is null)
        {
            return new SupportTriageTurnResult(
                state,
                "How would you prefer to be contacted?",
                false);
        }

        var completed = new SupportCaseState
        {
            IssueSummary = state.IssueSummary,
            Impact = state.Impact,
            ContactPreference = input,
            UpdatedAt = now,
        };
        return new SupportTriageTurnResult(completed, BuildSummary(completed), true);
    }

    private static string BuildSummary(SupportCaseState state) =>
        $"Your support case has been recorded:\n" +
        $"- Issue: {state.IssueSummary}\n" +
        $"- Impact: {state.Impact}\n" +
        $"- Contact preference: {state.ContactPreference}\n\n" +
        $"We will follow up with you shortly.";
}
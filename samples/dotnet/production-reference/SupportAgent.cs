// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace ProductionReference;

public sealed class SupportAgent : AgentApplication
{
    public SupportAgent(AgentApplicationOptions options, Telemetry telemetry) : base(options)
    {
        OnAfterTurn((context, state, cancellationToken) => Task.FromResult(true));
        OnConversationUpdate("membersAdded", async (context, state, cancellationToken) =>
        {
            if (context.Activity.MembersAdded?.Any(member => member.Id != context.Activity.Recipient?.Id) == true)
            {
                await context.SendActivityAsync(
                    "Welcome. Describe the support issue you want to report, for example: \"Checkout is unavailable.\"",
                    cancellationToken: cancellationToken);
            }
        });
        OnActivity(ActivityTypes.Message, async (context, state, cancellationToken) =>
        {
            using var span = telemetry.Source.StartActivity("agent.support_issue_capture.turn");
            try
            {
                var current = state.GetValue<IssueCaptureState>("conversation.supportIssueCapture") ?? new();
                var (updated, reply) = IssueCapture.Advance(current, context.Activity.Text ?? "");
                state.SetValue("conversation.supportIssueCapture", updated);
                await state.SaveStateAsync(context, cancellationToken: cancellationToken);
                telemetry.Turn();
                await context.SendActivityAsync(reply, cancellationToken: cancellationToken);
            }
            catch
            {
                span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
                throw;
            }
        });
    }
}

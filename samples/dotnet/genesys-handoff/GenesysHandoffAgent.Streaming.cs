// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Resilient Copilot Studio (MCS) SSE streaming with retry-with-backoff.
// Fixes: "Unable to read data from the transport connection: An existing
// connection was forcibly closed by the remote host." errors on long
// generative turns where an idle-sensitive intermediary RSTs the socket.

using GenesysHandoff.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff
{
    public partial class GenesysHandoffAgent
    {
        private const int McsStreamMaxAttempts = 3;
        private static readonly TimeSpan McsStreamBaseDelay = TimeSpan.FromMilliseconds(500);

        // Friendly fallback shown when the Copilot Studio stream cannot be completed after retries.
        private const string CopilotStreamErrorMessage =
            "Sorry, I hit a temporary connection problem while getting that answer. Please send your message again.";

        /// <summary>
        /// Handles starting a new conversation with Copilot Studio.
        /// StartConversation carries no user turn, so a clean retry on transient
        /// transport faults is always safe.
        /// </summary>
        private async Task<string> HandleNewConversation(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient,
            CancellationToken cancellationToken)
        {
            ConversationReference? lastCopilotStudioRef = null;

            for (var attempt = 1; attempt <= McsStreamMaxAttempts; attempt++)
            {
                try
                {
                    await foreach (IActivity activity in cpsClient.StartConversationAsync(
                        emitStartConversationEvent: true, cancellationToken: cancellationToken))
                    {
                        _logger.LogInformation(
                            "Activity from CPS (StartConversation): Id={CpsActivityId} ReplyToId={CpsReplyToId} Type={Type} Name={Name} Conversation={ConversationId}",
                            activity.Id, activity.ReplyToId, activity.Type, activity.Name, activity.Conversation?.Id);

                        lastCopilotStudioRef = activity.GetConversationReference();
                        if (activity.IsType(ActivityTypes.Message)
                            && !string.IsNullOrWhiteSpace(activity.Conversation?.Id))
                        {
                            _stateManager.SetConversationId(turnState, activity.Conversation.Id);
                        }
                    }

                    break; // success
                }
                catch (Exception ex) when (IsTransientStreamFault(ex) && !cancellationToken.IsCancellationRequested)
                {
                    if (attempt >= McsStreamMaxAttempts)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to start Copilot Studio conversation after {MaxAttempts} attempts. Surfacing fallback message.",
                            McsStreamMaxAttempts);
                        await turnContext.SendActivityAsync(CopilotStreamErrorMessage, cancellationToken: cancellationToken);
                        return string.Empty;
                    }

                    var delay = TimeSpan.FromMilliseconds(McsStreamBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogWarning(
                        ex,
                        "Transient reset starting Copilot Studio conversation on attempt {Attempt}/{MaxAttempts}. Retrying in {DelayMs} ms.",
                        attempt, McsStreamMaxAttempts, (int)delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (lastCopilotStudioRef != null)
            {
                _stateManager.SetLastCopilotStudioReference(turnState, lastCopilotStudioRef);
            }

            return lastCopilotStudioRef?.Conversation.Id ?? string.Empty;
        }

        /// <summary>
        /// Handles processing messages through Copilot Studio and checking for escalation events.
        /// Resilient version: transient transport resets on the SSE stream are retried
        /// (before any assistant reply is surfaced) and never leak to the end user.
        /// </summary>
        private async Task HandleCopilotStudioMessage(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient,
            string mcsConversationId,
            CancellationToken cancellationToken)
        {
            var lastCopilotStudioRef = _stateManager.GetLastCopilotStudioReference(turnState);
            _logger.LogInformation(
                "Activity from Teams: Id={TeamsActivityId} ReplyToId={TeamsReplyToId} Type={Type} Conversation={ConversationId}",
                turnContext.Activity.Id, turnContext.Activity.ReplyToId, turnContext.Activity.Type, mcsConversationId);

            var activityToSend = await BuildCopilotStudioActivityAsync(
                turnContext.Activity, lastCopilotStudioRef, mcsConversationId, cancellationToken);

            // Store the Teams conversation reference so proactive messages can be sent back.
            await _messageSender.StoreUserChannelReferenceAsync(turnContext.Activity, mcsConversationId, cancellationToken);

            ConversationReference? latestCopilotStudioRef = null;
            var assistantMessageSurfaced = false;
            var resetDuringTurn = false;

            for (var attempt = 1; attempt <= McsStreamMaxAttempts; attempt++)
            {
                try
                {
                    await foreach (IActivity activity in cpsClient.SendActivityAsync(activityToSend, cancellationToken))
                    {
                        latestCopilotStudioRef = activity.GetConversationReference();

                        // Track whether we've already shown the user a real reply this turn.
                        // Once we have, a later transport fault must NOT trigger a retry
                        // (that would re-send the user's turn and double-post).
                        if (activity.IsType(ActivityTypes.Message) || activity.IsType(ActivityTypes.InvokeResponse))
                        {
                            assistantMessageSurfaced = true;
                        }

                        var reset = await ProcessCopilotStudioActivityAsync(
                            turnContext, turnState, activity, mcsConversationId, cancellationToken);
                        if (reset)
                        {
                            resetDuringTurn = true;
                            break; // conversation was reset; stop processing further CPS activities.
                        }
                    }

                    break; // stream completed successfully
                }
                catch (Exception ex) when (IsTransientStreamFault(ex) && !cancellationToken.IsCancellationRequested)
                {
                    if (assistantMessageSurfaced || attempt >= McsStreamMaxAttempts)
                    {
                        _logger.LogError(
                            ex,
                            "Copilot Studio stream failed for conversation {ConversationId} on attempt {Attempt}/{MaxAttempts} " +
                            "(assistantMessageSurfaced={Surfaced}). Surfacing fallback message.",
                            mcsConversationId, attempt, McsStreamMaxAttempts, assistantMessageSurfaced);

                        await turnContext.SendActivityAsync(CopilotStreamErrorMessage, cancellationToken: cancellationToken);
                        break;
                    }

                    var delay = TimeSpan.FromMilliseconds(McsStreamBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogWarning(
                        ex,
                        "Transient Copilot Studio stream reset for conversation {ConversationId} on attempt {Attempt}/{MaxAttempts}. " +
                        "Retrying in {DelayMs} ms.",
                        mcsConversationId, attempt, McsStreamMaxAttempts, (int)delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (latestCopilotStudioRef != null && !resetDuringTurn)
            {
                _stateManager.SetLastCopilotStudioReference(turnState, latestCopilotStudioRef);
            }
        }

        /// <summary>
        /// True when the exception represents a transient transport-level fault on the
        /// Copilot Studio SSE stream that is safe to retry at the connection level.
        /// </summary>
        private static bool IsTransientStreamFault(Exception ex)
        {
            for (var current = ex; current is not null; current = current.InnerException)
            {
                switch (current)
                {
                    case SocketException se
                        when se.SocketErrorCode is SocketError.ConnectionReset
                                                or SocketError.ConnectionAborted
                                                or SocketError.TimedOut:
                        return true;
                    case IOException io when io.InnerException is SocketException:
                        return true;
                    case HttpRequestException:
                        return true;
                    case OperationCanceledException oce when !oce.CancellationToken.IsCancellationRequested:
                        return true;
                }
            }

            return false;
        }
    }
}

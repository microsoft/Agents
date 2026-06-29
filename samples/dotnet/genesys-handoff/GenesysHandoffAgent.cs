// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GenesysHandoff.Genesys;
using GenesysHandoff.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace GenesysHandoff
{
    /// <summary>
    /// An AgentApplication that integrates with Genesys for human handoff.
    /// </summary>
    public class GenesysHandoffAgent : AgentApplication
    {
        private const string McsHandlerName = "mcs";

        private readonly string _endLiveChatMessage;
        private readonly string _messageSendErrorMessage;
        private readonly string _escalationErrorMessage;
        private readonly string _disconnectFromLiveAgentMessage;
        private readonly string _userDisconnectedMessage;
        private readonly string _signOutMessage;
        private readonly string _signInFailureFormat;
        private readonly GenesysMessageSender _messageSender;
        private readonly CopilotClientFactory _copilotClientFactory;
        private readonly ActivityResponseProcessor _responseProcessor;
        private readonly IActivityReplyMappingStore _activityReplyMappingStore;
        private readonly ConversationStateManager _stateManager;
        private readonly ConversationResetService _resetService;
        private readonly GenesysNotificationService? _notificationService;
        private readonly ILogger<GenesysHandoffAgent> _logger;
        private readonly IStorage _storage;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysHandoffAgent"/> class.
        /// </summary>
        public GenesysHandoffAgent(
            AgentApplicationOptions options,
            GenesysMessageSender messageSender,
            CopilotClientFactory copilotClientFactory,
            ActivityResponseProcessor responseProcessor,
            IActivityReplyMappingStore activityReplyMappingStore,
            ConversationStateManager stateManager,
            IGenesysConnectionSettings settings,
            ConversationResetService resetService,
            ILogger<GenesysHandoffAgent> logger,
            IStorage storage,
            IConfiguration configuration,
            GenesysNotificationService? notificationService = null) : base(options)
        {
            _endLiveChatMessage = settings.EndLiveChatMessage ?? "End chat with agent";
            _messageSendErrorMessage = configuration?.GetSection("TeamsMessages:MessageSendErrorMessage")?.Value ?? "Sorry, there was a problem sending your message to the live agent. Please try again.";
            _escalationErrorMessage = configuration?.GetSection("TeamsMessages:EscalationErrorMessage")?.Value ?? "Sorry, we couldn't connect you to a live agent at this time. Please try again later.";
            _disconnectFromLiveAgentMessage = configuration?.GetSection("TeamsMessages:DisconnectFromLiveAgentMessage")?.Value ?? "You have ended the chat with the live agent.";
            _userDisconnectedMessage = settings.UserDisconnectedMessage ?? "The user has disconnected from the conversation.";
            _signOutMessage = configuration?.GetSection("TeamsMessages:SignOutMessage")?.Value ?? "You have signed out";
            _signInFailureFormat = configuration?.GetSection("TeamsMessages:SignInFailureFormat")?.Value ?? "SignIn failed with '{0}': {1}/{2}";
            _messageSender = messageSender;
            _copilotClientFactory = copilotClientFactory;
            _responseProcessor = responseProcessor;
            _activityReplyMappingStore = activityReplyMappingStore;
            _stateManager = stateManager;
            _resetService = resetService;
            _logger = logger;
            _notificationService = notificationService;
            _storage = storage;

            OnMessage("-reset", HandleResetMessage);
            OnMessage("-signout", HandleSignOut);
            AddRoute((turnContext, cancellationToken) => Task.FromResult(true), HandleAllActivities, autoSignInHandlers: [McsHandlerName]);
            UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
            {
                var failureMessage = string.Format(_signInFailureFormat, handlerName, response.Cause, response.Error!.Message);
                await turnContext.SendActivityAsync(failureMessage, cancellationToken: cancellationToken);
            });
        }

        /// <summary>
        /// Handles all incoming activities for the current conversation, including starting new conversations,
        /// processing messages, and managing escalation to a human agent as needed.
        /// </summary>
        /// <param name="turnContext">The context object for the current turn of the conversation.</param>
        /// <param name="turnState">The state object containing conversation-scoped properties.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleAllActivities(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await HandleAllActivitiesInternal(turnContext, turnState, cancellationToken, skipIdempotencyCheck: false);
        }

        private async Task HandleAllActivitiesInternal(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken, bool skipIdempotencyCheck)
        {
            var mcsConversationId = _stateManager.GetConversationId(turnState);
            var cpsClient = _copilotClientFactory.CreateClient(this, turnContext);

            // Idempotency: skip if this activity ID was already processed for this conversation scope.
            if (!skipIdempotencyCheck && !string.IsNullOrWhiteSpace(turnContext.Activity.Id))
            {
                var conversationScope = mcsConversationId
                    ?? turnContext.Activity.Conversation?.Id
                    ?? "unknown";
                var idempotencyKey = $"idempotency:{conversationScope}:{turnContext.Activity.Id}";
                var idempotencyState = await _storage.ReadAsync([idempotencyKey], cancellationToken);
                if (idempotencyState.TryGetValue(idempotencyKey, out _))
                {
                    _logger.LogWarning("Duplicate activity detected: {ActivityId} for conversation {ConversationId}. Skipping.", turnContext.Activity.Id, conversationScope);
                    return;
                }

                await _storage.WriteAsync(new Dictionary<string, object> { [idempotencyKey] = new { processed = true } }, cancellationToken);
            }

            if (!string.IsNullOrEmpty(mcsConversationId)
                && await _resetService.CheckAndClearResetRequestedAsync(mcsConversationId, cancellationToken))
            {
                _logger.LogInformation("Conversation reset was requested for {ConversationId}; starting a fresh Copilot Studio conversation.", mcsConversationId);
                _stateManager.ClearConversationState(turnState);
                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                mcsConversationId = await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
            }

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                // Check whether the user already has an ongoing CPS conversation recorded in state.
                // If so, stitch the new activity into that conversation instead of starting a fresh one.
                mcsConversationId = await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
            }
            var isMessage = turnContext.Activity.IsType(ActivityTypes.Message);
            var isInvoke = turnContext.Activity.IsType(ActivityTypes.Invoke);
            var isMessageUpdate = turnContext.Activity.IsType(ActivityTypes.MessageUpdate);

            if (isMessage || isInvoke || isMessageUpdate)
            {
                var isEscalated = _stateManager.IsEscalated(turnState);
                if (isEscalated && !isInvoke)
                {
                    await HandleEscalatedMessageAsync(turnContext, turnState, cpsClient, mcsConversationId, cancellationToken);
                }
                else if (isMessage || isInvoke)
                {
                    await HandleCopilotStudioMessage(turnContext, turnState, cpsClient, mcsConversationId, cancellationToken);
                }
            }
        }

        private async Task HandleEscalatedMessageAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient,
            string mcsConversationId,
            CancellationToken cancellationToken)
        {
            // Check if the agent has disconnected since our last turn.
            if (_notificationService != null
                && await _notificationService.CheckAndClearAgentDisconnectedAsync(mcsConversationId, cancellationToken))
            {
                // Agent disconnected — reset state and start a fresh CPS session (same as -reset).
                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                await _stateManager.ClearEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);
                _stateManager.ClearConversationState(turnState);
                await HandleAllActivitiesInternal(turnContext, turnState, cancellationToken, skipIdempotencyCheck: true);
                return;
            }

            // Check if the user clicked the "End chat with agent" suggested action.
            if (string.Equals(turnContext.Activity.Text, _endLiveChatMessage, StringComparison.OrdinalIgnoreCase))
            {
                await DisconnectFromLiveAgent(turnContext, turnState, mcsConversationId, cancellationToken);
                return;
            }

            try
            {
                await _messageSender.SendMessageToGenesysAsync(turnContext.Activity, mcsConversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward message to Genesys for conversation {ConversationId}.", mcsConversationId);
                await turnContext.SendActivityAsync(_messageSendErrorMessage, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Handles starting a new conversation with Copilot Studio.
        /// The last activity received from CPS is stored in conversation state so that
        /// subsequent turns can be stitched to this conversation.
        /// </summary>
        private async Task<string> HandleNewConversation(ITurnContext turnContext, ITurnState turnState, Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient, CancellationToken cancellationToken)
        {
            ConversationReference? lastCopilotStudioRef = null;

            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                _logger.LogInformation(
                    "Activity from CPS (StartConversation): Id={CpsActivityId} ReplyToId={CpsReplyToId} Type={Type} Name={Name} Conversation={ConversationId}",
                    activity.Id, activity.ReplyToId, activity.Type, activity.Name, activity.Conversation?.Id);
                lastCopilotStudioRef = activity.GetConversationReference();
                if (activity.IsType(ActivityTypes.Message))
                {
                    //var responseActivity = _responseProcessor.CreateResponseActivity(activity, "StartConversation");
                  //await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(activity.Conversation?.Id))
                    {
                        _stateManager.SetConversationId(turnState, activity.Conversation.Id);
                    }
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
        /// The last activity received from CPS is stored in conversation state so that
        /// subsequent turns can be stitched to this conversation.
        /// </summary>
        private async Task HandleCopilotStudioMessage(ITurnContext turnContext, ITurnState turnState, Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient, string mcsConversationId, CancellationToken cancellationToken)
        {
            // When a message is received from the user, it is forwarded to Copilot Studio using the conversation ID stored in state.
            // The agent then listens for responses from Copilot Studio. If a message activity is received, it is sent back to the user.
            // If an event activity with the name "GenesysHandoff" is received, it indicates that the conversation should be escalated to a human agent through Genesys.
            var lastCopilotStudioRef = _stateManager.GetLastCopilotStudioReference(turnState);
            _logger.LogInformation(
                "Activity from Teams: Id={TeamsActivityId} ReplyToId={TeamsReplyToId} Type={Type} Conversation={ConversationId}",
                turnContext.Activity.Id, turnContext.Activity.ReplyToId, turnContext.Activity.Type, mcsConversationId);
            var activityToSend = await BuildCopilotStudioActivityAsync(turnContext.Activity, lastCopilotStudioRef, mcsConversationId, cancellationToken);

            // Store the Teams conversation reference so proactive messages (e.g. from the reset API) can be sent back.
            await _messageSender.StoreUserChannelReferenceAsync(turnContext.Activity, mcsConversationId, cancellationToken);
            ConversationReference? latestCopilotStudioRef = null;
            await foreach (IActivity activity in cpsClient.SendActivityAsync(activityToSend, cancellationToken))
            {
                latestCopilotStudioRef = activity.GetConversationReference();
                var result = await ProcessCopilotStudioActivityAsync(turnContext, turnState, activity, mcsConversationId, cancellationToken);
                if (result) break; // If true is returned, it indicates the conversation has been reset and we should stop processing further CPS activities for this turn.
            }

            if (latestCopilotStudioRef != null)
            {
                _stateManager.SetLastCopilotStudioReference(turnState, latestCopilotStudioRef);
            }
        }

        private async Task<bool> ProcessCopilotStudioActivityAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            IActivity activity,
            string mcsConversationId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Activity from CPS: Id={CpsActivityId} ReplyToId={CpsReplyToId} Type={Type} Name={Name} Conversation={ConversationId}",
                activity.Id, activity.ReplyToId, activity.Type, activity.Name, mcsConversationId);

            if (activity.IsType(ActivityTypes.Message))
            {
                var responseActivity = _responseProcessor.CreateResponseActivity(activity, "AskQuestion");
                var resourceResponse = await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                _logger.LogInformation(
                    "Activity to Teams: TeamsActivityId={TeamsActivityId} SourceCpsId={CpsActivityId} Type=message Conversation={ConversationId}",
                    resourceResponse?.Id, activity.Id, mcsConversationId);
                if (!string.IsNullOrWhiteSpace(activity.Id) && !string.IsNullOrWhiteSpace(resourceResponse?.Id))
                {
                    await _activityReplyMappingStore.UpsertAsync(mcsConversationId, resourceResponse.Id, activity.Id, cancellationToken);
                }

                return false;
            }

            if (activity.IsType(ActivityTypes.InvokeResponse))
            {
                var responseActivity = _responseProcessor.CreateInvokeResponseActivity(activity, "InvokeResponse");
                var resourceResponse = await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                _logger.LogInformation(
                    "Activity to Teams: TeamsActivityId={TeamsActivityId} SourceCpsId={CpsActivityId} Type=invokeResponse Conversation={ConversationId}",
                    resourceResponse?.Id, activity.Id, mcsConversationId);
                return false;
            }

            if (activity.IsType(ActivityTypes.Event) && string.Equals(activity.Name, "EndOfConversation", StringComparison.Ordinal))
            {
                await HandleResetMessage(turnContext, turnState, cancellationToken);
                return true;
            }

            if (activity.IsType(ActivityTypes.Event) && string.Equals(activity.Name, "GenesysHandoff", StringComparison.Ordinal))
            {
                await HandleEscalation(turnContext, turnState, activity, mcsConversationId, cancellationToken);
            }
            return false;
        }

        /// <summary>
        /// Handles escalation to a human agent through Genesys.
        /// </summary>
        private async Task HandleEscalation(ITurnContext turnContext, ITurnState turnState, IActivity activity, string mcsConversationId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Escalating conversation {ConversationId} to Genesys.", mcsConversationId);
            _stateManager.SetEscalated(turnState, true);
            var summarizationActivity = turnContext.Activity.GetConversationReference().GetContinuationActivity();
            summarizationActivity.Text = activity.Value?.ToString() ?? "The chat is being escalated to a human agent.";

            try
            {
                var genesysConversationId = await _messageSender.SendMessageToGenesysAsync(summarizationActivity, mcsConversationId, cancellationToken, prefetchConversationId: true);

                // Persist an escalation marker keyed by the MCS conversation ID so the external reset API
                // can tell the conversation is escalated (turn state alone is not visible to that endpoint).
                await _stateManager.SetEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);

                // Subscribe to agent disconnect notifications if the notification service is enabled
                if (_notificationService != null && !string.IsNullOrEmpty(genesysConversationId))
                {
                    _stateManager.SetGenesysConversationId(turnState, genesysConversationId);
                    await _notificationService.SubscribeToConversationEventsAsync(genesysConversationId, mcsConversationId, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to escalate conversation {ConversationId} to Genesys.", mcsConversationId);
                _stateManager.SetEscalated(turnState, false);
                await _stateManager.ClearEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);
                await turnContext.SendActivityAsync(_escalationErrorMessage, cancellationToken: cancellationToken);
            }
        }

    

        private async Task CleanupConversationResourcesAsync(ITurnState turnState, string? mcsConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mcsConversationId))
            {
                return;
            }

            try
            {
                var genesysConversationId = _stateManager.GetGenesysConversationId(turnState);
                if (_stateManager.IsEscalated(turnState) && !string.IsNullOrEmpty(genesysConversationId))
                {
                    await _messageSender.DisconnectConversationAsync(genesysConversationId, cancellationToken);
                }

                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                await _stateManager.ClearEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);

                if (_notificationService != null && !string.IsNullOrEmpty(genesysConversationId))
                {
                    await _notificationService.UnsubscribeFromConversationEventsAsync(genesysConversationId, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup for conversation {ConversationId}. Continuing with reset.", mcsConversationId);
            }
        }

        /// <summary>
        /// Builds an activity suitable for sending to Copilot Studio, copying user-relevant
        /// properties from the incoming activity onto the continuation or fallback activity.
        /// </summary>
        private async Task<Activity> BuildCopilotStudioActivityAsync(IActivity incomingActivity, ConversationReference? cpsReference, string mcsConversationId, CancellationToken cancellationToken)
        {
            Activity activityToSend;
            if (cpsReference != null && !string.IsNullOrEmpty(cpsReference.Conversation?.Id))
            {
                activityToSend = cpsReference.GetContinuationActivity();
            }
            else
            {
                // No valid CPS reference in state (e.g., after deployment/upgrade or partial state loss).
                activityToSend = new Activity
                {
                    Type = incomingActivity.Type,
                    Conversation = new ConversationAccount { Id = mcsConversationId },
                    ChannelId = incomingActivity.ChannelId,
                    ServiceUrl = incomingActivity.ServiceUrl,
                    Recipient = incomingActivity.Recipient,
                };
            }

            // Assign a fresh Id so the outbound activity is not confused with the prior MCS turn
            // whose Id GetContinuationActivity() copies from the stored ConversationReference.
            activityToSend.Id = Guid.NewGuid().ToString();
            activityToSend.From = incomingActivity.From;
            activityToSend.Type = incomingActivity.Type;
            activityToSend.Text = incomingActivity.Text;
            activityToSend.Attachments = incomingActivity.Attachments;
            activityToSend.Entities = incomingActivity.Entities;
            activityToSend.Value = incomingActivity.Value;
            activityToSend.Name = incomingActivity.Name;
            activityToSend.ValueType = incomingActivity.ValueType;

            // Map ReplyToId if present
            if (!string.IsNullOrWhiteSpace(incomingActivity.ReplyToId))
            {
                var mcsReplyToId = await _activityReplyMappingStore.GetMcsActivityIdAsync(mcsConversationId, incomingActivity.ReplyToId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(mcsReplyToId))
                {
                    activityToSend.ReplyToId = mcsReplyToId;
                }
            }

            _logger.LogInformation(
                "Activity to CPS: Id={CpsActivityId} ReplyToId={CpsReplyToId} Type={Type} SourceTeamsId={TeamsActivityId} SourceTeamsReplyToId={TeamsReplyToId} Conversation={ConversationId}",
                activityToSend.Id, activityToSend.ReplyToId, activityToSend.Type, incomingActivity.Id, incomingActivity.ReplyToId, mcsConversationId);

            return activityToSend;
        }

        /// <summary>
        /// Disconnects from the live agent, cleans up Genesys resources, resets state,
        /// and starts a new Copilot Studio conversation.
        /// </summary>
        private async Task DisconnectFromLiveAgent(ITurnContext turnContext, ITurnState turnState, string mcsConversationId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User initiated disconnect from live agent for conversation {ConversationId}.", mcsConversationId);

            try
            {
                var genesysConversationId = _stateManager.GetGenesysConversationId(turnState);
                if (!string.IsNullOrEmpty(genesysConversationId))
                {
                    // Let the live agent know the user has left before the conversation is torn down.
                    await NotifyLiveAgentUserDisconnectedAsync(turnContext, mcsConversationId, cancellationToken);

                    await _messageSender.DisconnectConversationAsync(genesysConversationId, cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.UnsubscribeFromConversationEventsAsync(genesysConversationId, cancellationToken);
                    }
                }

                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                await _stateManager.ClearEscalatedInStorageAsync(_storage, mcsConversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during live agent disconnect cleanup for conversation {ConversationId}. Continuing with reset.", mcsConversationId);
            }

            _stateManager.ClearConversationState(turnState);
            await turnContext.SendActivityAsync(_disconnectFromLiveAgentMessage, cancellationToken: cancellationToken);

            // Start a fresh Copilot Studio conversation
            var cpsClient = _copilotClientFactory.CreateClient(this, turnContext);
            await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
        }

        /// <summary>
        /// Sends a message to the live agent (via Genesys) informing them that the Teams user has
        /// disconnected from the escalated conversation. Failures are logged but do not block the disconnect.
        /// </summary>
        private async Task NotifyLiveAgentUserDisconnectedAsync(ITurnContext turnContext, string mcsConversationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mcsConversationId) || string.IsNullOrWhiteSpace(_userDisconnectedMessage))
            {
                return;
            }

            try
            {
                var notice = turnContext.Activity.GetConversationReference().GetContinuationActivity();
                notice.Text = _userDisconnectedMessage;
                await _messageSender.SendMessageToGenesysAsync(notice, mcsConversationId, cancellationToken);
                _logger.LogInformation("Notified live agent that the user disconnected for conversation {ConversationId}.", mcsConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify live agent of user disconnect for conversation {ConversationId}. Continuing with disconnect.", mcsConversationId);
            }
        }

        /// <summary>
        /// Signs the user out of the current session and sends a confirmation message to the user.
        /// </summary>
        /// <param name="turnContext">The context object for the current turn of the conversation. Provides information about the incoming
        /// activity and allows sending activities to the user.</param>
        /// <param name="turnState">The state object for the current turn, containing shared data and services relevant to the turn's
        /// processing.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to propagate notification that the operation should be canceled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleSignOut(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync(_signOutMessage, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Resets the conversation state and notifies the user that the conversation has been reset.
        /// </summary>
        /// <param name="turnContext">The context object for the current turn of the conversation.</param>
        /// <param name="turnState">The state object containing conversation-specific properties to be cleared.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleResetMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var mcsConversationId = _stateManager.GetConversationId(turnState);
            if (!string.IsNullOrEmpty(mcsConversationId))
            {
                await _resetService.ResetConversationAsync(mcsConversationId, null, cancellationToken);
            }

            await CleanupConversationResourcesAsync(turnState, mcsConversationId, cancellationToken);

            _stateManager.ClearConversationState(turnState);
        }
    }
}
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff
{
    /// <summary>
    /// An AgentApplication that integrates with Genesys for human handoff.
    /// </summary>
    public class GenesysHandoffAgent : AgentApplication
    {
        private const string McsHandlerName = "mcs";
        private const string EndLiveChatAction = "End chat with agent";

        private readonly GenesysMessageSender _messageSender;
        private readonly CopilotClientFactory _copilotClientFactory;
        private readonly ActivityResponseProcessor _responseProcessor;
        private readonly ConversationStateManager _stateManager;
        private readonly GenesysNotificationService? _notificationService;
        private readonly ILogger<GenesysHandoffAgent> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysHandoffAgent"/> class.
        /// </summary>
        public GenesysHandoffAgent(
            AgentApplicationOptions options,
            GenesysMessageSender messageSender,
            CopilotClientFactory copilotClientFactory,
            ActivityResponseProcessor responseProcessor,
            ConversationStateManager stateManager,
            ILogger<GenesysHandoffAgent> logger,
            GenesysNotificationService? notificationService = null) : base(options)
        {
            _messageSender = messageSender;
            _copilotClientFactory = copilotClientFactory;
            _responseProcessor = responseProcessor;
            _stateManager = stateManager;
            _logger = logger;
            _notificationService = notificationService;

            OnMessage("-reset", HandleResetMessage);
            OnMessage("-signout", HandleSignOut);
            AddRoute((turnContext, cancellationToken) => Task.FromResult(true), HandleAllActivities, autoSignInHandlers: [McsHandlerName]);
            UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
            {
                await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
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
            var mcsConversationId = _stateManager.GetConversationId(turnState);
            var cpsClient = _copilotClientFactory.CreateClient(this, turnContext);

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                // Check whether the user already has an ongoing CPS conversation recorded in state.
                // If so, stitch the new activity into that conversation instead of starting a fresh one.
                var lastCopilotStudioRef = _stateManager.GetLastCopilotStudioReference(turnState);
                if (lastCopilotStudioRef != null
                    && !string.IsNullOrEmpty(lastCopilotStudioRef.Conversation?.Id)
                    && (turnContext.Activity.IsType(ActivityTypes.Message) || turnContext.Activity.IsType(ActivityTypes.Invoke)))
                {
                    var existingConversationId = lastCopilotStudioRef.Conversation.Id;
                    _stateManager.SetConversationId(turnState, existingConversationId);
                    await HandleCopilotStudioMessage(turnContext, turnState, cpsClient, existingConversationId, cancellationToken);
                }
                else
                {
                    await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
                }
            }
            else if (turnContext.Activity.IsType(ActivityTypes.Message) || turnContext.Activity.IsType(ActivityTypes.Invoke))
            {
                var isEscalated = _stateManager.IsEscalated(turnState);
                if (isEscalated)
                {
                    // Check if the agent has disconnected since our last turn
                    if (_notificationService != null
                        && await _notificationService.CheckAndClearAgentDisconnectedAsync(mcsConversationId, cancellationToken))
                    {
                        // Agent disconnected — reset state and start a fresh CPS session (same as -reset)
                        await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
                        _stateManager.ClearConversationState(turnState);
                        await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
                    }
                    else
                    {
                        // Check if the user clicked the "End chat with agent" suggested action
                        if (string.Equals(turnContext.Activity.Text, EndLiveChatAction, StringComparison.OrdinalIgnoreCase))
                        {
                            await DisconnectFromLiveAgent(turnContext, turnState, mcsConversationId, cancellationToken);
                        }
                        else
                        {
                            try
                            {
                                await _messageSender.SendMessageToGenesysAsync(turnContext.Activity, mcsConversationId, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to forward message to Genesys for conversation {ConversationId}.", mcsConversationId);
                                await turnContext.SendActivityAsync("Sorry, there was a problem sending your message to the live agent. Please try again.", cancellationToken: cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    await HandleCopilotStudioMessage(turnContext, turnState, cpsClient, mcsConversationId, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles starting a new conversation with Copilot Studio.
        /// The last activity received from CPS is stored in conversation state so that
        /// subsequent turns can be stitched to this conversation.
        /// </summary>
        private async Task HandleNewConversation(ITurnContext turnContext, ITurnState turnState, Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient, CancellationToken cancellationToken)
        {
            ConversationReference? lastCopilotStudioRef = null;

            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                lastCopilotStudioRef = activity.GetConversationReference();
                if (activity.IsType(ActivityTypes.Message))
                {
                    var responseActivity = _responseProcessor.CreateResponseActivity(activity, "StartConversation");
                    await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                    _stateManager.SetConversationId(turnState, activity.Conversation.Id);
                }
            }

            if (lastCopilotStudioRef != null)
            {
                _stateManager.SetLastCopilotStudioReference(turnState, lastCopilotStudioRef);
            }
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
            var activityToSend = BuildCopilotStudioActivity(turnContext.Activity, lastCopilotStudioRef, mcsConversationId);
            ConversationReference? latestCopilotStudioRef = null;
            await foreach (IActivity activity in cpsClient.SendActivityAsync(activityToSend, cancellationToken))
            {
                latestCopilotStudioRef = activity.GetConversationReference();

                if (activity.IsType(ActivityTypes.Message))
                { 
                    var responseActivity = _responseProcessor.CreateResponseActivity(activity, "AskQuestion");
                    await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                }
                else if (activity.IsType(ActivityTypes.InvokeResponse))
                {
                    var responseActivity = _responseProcessor.CreateInvokeResponseActivity(activity, "InvokeResponse");
                    await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                }
                else if (activity.IsType(ActivityTypes.Event) && string.Equals(activity.Name, "GenesysHandoff", StringComparison.Ordinal))
                {

                    await HandleEscalation(turnContext, turnState, activity, mcsConversationId, cancellationToken);
                }
            }

            if (latestCopilotStudioRef != null)
            {
                _stateManager.SetLastCopilotStudioReference(turnState, latestCopilotStudioRef);
            }
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
                await turnContext.SendActivityAsync("Sorry, we couldn't connect you to a live agent at this time. Please try again later.", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Builds an activity suitable for sending to Copilot Studio, copying user-relevant
        /// properties from the incoming activity onto the continuation or fallback activity.
        /// </summary>
        private static Activity BuildCopilotStudioActivity(IActivity incomingActivity, ConversationReference? cpsReference, string mcsConversationId)
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

            activityToSend.From = incomingActivity.From;
            activityToSend.Type = incomingActivity.Type;
            activityToSend.Text = incomingActivity.Text;
            activityToSend.Attachments = incomingActivity.Attachments;
            activityToSend.Entities = incomingActivity.Entities;
            activityToSend.Value = incomingActivity.Value;
            activityToSend.Name = incomingActivity.Name;
            activityToSend.ValueType = incomingActivity.ValueType;

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
                    await _messageSender.DisconnectConversationAsync(genesysConversationId, cancellationToken);

                    if (_notificationService != null)
                    {
                        await _notificationService.UnsubscribeFromConversationEventsAsync(genesysConversationId, cancellationToken);
                    }
                }

                await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during live agent disconnect cleanup for conversation {ConversationId}. Continuing with reset.", mcsConversationId);
            }

            _stateManager.ClearConversationState(turnState);
            await turnContext.SendActivityAsync("You have ended the chat with the live agent.", cancellationToken: cancellationToken);

            // Start a fresh Copilot Studio conversation
            var cpsClient = _copilotClientFactory.CreateClient(this, turnContext);
            await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
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
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
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
                try
                {
                    // If currently escalated to a live agent, disconnect the Genesys conversation first
                    var genesysConversationId = _stateManager.GetGenesysConversationId(turnState);
                    if (_stateManager.IsEscalated(turnState) && !string.IsNullOrEmpty(genesysConversationId))
                    {
                        await _messageSender.DisconnectConversationAsync(genesysConversationId, cancellationToken);
                    }

                    await _messageSender.DeleteUserChannelReferenceAsync(mcsConversationId, cancellationToken);

                    if (_notificationService != null && !string.IsNullOrEmpty(genesysConversationId))
                    {
                        await _notificationService.UnsubscribeFromConversationEventsAsync(genesysConversationId, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup for conversation reset {ConversationId}. Continuing with reset.", mcsConversationId);
                }
            }

            _stateManager.ClearConversationState(turnState);
            await turnContext.SendActivityAsync("The conversation has been reset.", cancellationToken: cancellationToken);
        }
    }
}
using GenesysHandoff.Genesys;
using GenesysHandoff.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
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

        private readonly GenesysService _genesysService;
        private readonly CopilotClientFactory _copilotClientFactory;
        private readonly ActivityResponseProcessor _responseProcessor;
        private readonly ConversationStateManager _stateManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysHandoffAgent"/> class.
        /// </summary>
        /// <param name="options">The options for the agent application.</param>
        /// <param name="genesysService">The Genesys service for handling interactions.</param>
        /// <param name="copilotClientFactory">The factory for creating Copilot Studio clients.</param>
        /// <param name="responseProcessor">The processor for handling activity responses.</param>
        /// <param name="stateManager">The manager for conversation state.</param>
        public GenesysHandoffAgent(
            AgentApplicationOptions options,
            GenesysService genesysService,
            CopilotClientFactory copilotClientFactory,
            ActivityResponseProcessor responseProcessor,
            ConversationStateManager stateManager) : base(options)
        {
            _genesysService = genesysService;
            _copilotClientFactory = copilotClientFactory;
            _responseProcessor = responseProcessor;
            _stateManager = stateManager;

            OnMessage("-reset", HandleResetMessage);
            OnMessage("-signout", HandleSignOut);
            OnActivity((turnContext, cancellationToken) => Task.FromResult(true), HandleAllActivities, autoSignInHandlers: [McsHandlerName]);
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
                await HandleNewConversation(turnContext, turnState, cpsClient, cancellationToken);
            }
            else if (turnContext.Activity.IsType(ActivityTypes.Message))
            {
                var isEscalated = _stateManager.IsEscalated(turnState);
                if (isEscalated)
                {
                    await _genesysService.SendMessageToGenesysAsync(turnContext.Activity, mcsConversationId, cancellationToken);
                }
                else
                {
                    await HandleCopilotStudioMessage(turnContext, turnState, cpsClient, mcsConversationId, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles starting a new conversation with Copilot Studio.
        /// </summary>
        private async Task HandleNewConversation(ITurnContext turnContext, ITurnState turnState, Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient, CancellationToken cancellationToken)
        {
            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    var responseActivity = _responseProcessor.CreateResponseActivity(activity, "StartConversation");
                    await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                    _stateManager.SetConversationId(turnState, activity.Conversation.Id);
                }
            }
        }

        /// <summary>
        /// Handles processing messages through Copilot Studio and checking for escalation events.
        /// </summary>
        private async Task HandleCopilotStudioMessage(ITurnContext turnContext, ITurnState turnState, Microsoft.Agents.CopilotStudio.Client.CopilotClient cpsClient, string mcsConversationId, CancellationToken cancellationToken)
        {
            /**
             * When a message is received from the user, it is forwarded to Copilot Studio using the conversation ID stored in state.
             * The agent then listens for responses from Copilot Studio. If a message activity is received, it is sent back to the user.
             * If an event activity with the name "GenesysHandoff" is received, it indicates that the conversation should be escalated to a human agent through Genesys.
             */
            var activityToMcs = new Activity
            {
                Type = ActivityTypes.Message,
                Text = turnContext.Activity.Text,
                Conversation = new ConversationAccount { Id = mcsConversationId },
                Attachments = turnContext.Activity.Attachments,
                Entities = turnContext.Activity.Entities,
                Value = turnContext.Activity.Value,
                ValueType = turnContext.Activity.ValueType,
            };

            await foreach (IActivity activity in cpsClient.SendActivityAsync(activityToMcs, cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    var responseActivity = _responseProcessor.CreateResponseActivity(activity, "AskQuestion");
                    await turnContext.SendActivityAsync(responseActivity, cancellationToken);
                }

                if (activity.IsType(ActivityTypes.Event) && activity.Name.Equals("GenesysHandoff"))
                {
                    await HandleEscalation(turnContext, turnState, activity, mcsConversationId, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles escalation to a human agent through Genesys.
        /// </summary>
        private async Task HandleEscalation(ITurnContext turnContext, ITurnState turnState, IActivity activity, string mcsConversationId, CancellationToken cancellationToken)
        {
            _stateManager.SetEscalated(turnState, true);
            var summarizationActivity = turnContext.Activity.GetConversationReference().GetContinuationActivity();
            summarizationActivity.Text = activity.Value?.ToString() ?? "The chat is being escalated to a human agent.";
            await _genesysService.SendMessageToGenesysAsync(summarizationActivity, mcsConversationId, cancellationToken);
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
                await _genesysService.DeleteConversationReferenceAsync(mcsConversationId, cancellationToken);
            }

            _stateManager.ClearConversationState(turnState);
            await turnContext.SendActivityAsync("The conversation has been reset.", cancellationToken: cancellationToken);
        }
    }
}
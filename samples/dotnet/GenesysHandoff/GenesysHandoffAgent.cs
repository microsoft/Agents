using GenesysHandoff.Genesys;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GenesysHandoff
{
    /// <summary>
    /// An AgentApplication that integrates with Genesys for human handoff.
    /// </summary>
    public class GenesysHandoffAgent : AgentApplication
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly GenesysService _genesysService;
        const string MCSConversationPropertyName = "MCSConversationId"; // Property name to store the Copilot Studio conversation ID
        const string IsEscalatedPropertyName = "IsEscalated"; // Property name to indicate if the conversation has been escalated to a human agent

        /// <summary>
        /// Initializes a new instance of the <see cref="GenesysHandoffAgent"/> class.
        /// </summary>
        /// <param name="options">The options for the agent application.</param>
        /// <param name="httpClientFactory">The HTTP client factory for making API calls.</param>
        /// <param name="configuration">The configuration settings for the agent.</param>
        /// <param name="genesysService">The Genesys service for handling interactions.</param>
        public GenesysHandoffAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory, IConfiguration configuration, GenesysService genesysService) : base(options)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _genesysService = genesysService;
            OnMessage("-reset", HandleResetMessage);
            OnMessage("-signout", HandleSignOut);
            OnActivity((turnContext, cancellationToken) => Task.FromResult(true), HandleAllActivities, autoSignInHandlers: ["mcs"]);
            UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
            {
                await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
            });
        }

        /// <summary>
        /// Handles all incoming activities for the current conversation, including starting new conversations,
        /// processing messages, and managing escalation to a human agent as needed.
        /// </summary>
        /// <remarks>This method coordinates between automated and human agent handling based on the
        /// conversation state. If escalation is triggered, subsequent messages are forwarded to a human agent service.
        /// Otherwise, messages are processed by the Copilot Studio client. The method supports both starting new
        /// conversations and continuing existing ones.</remarks>
        /// <param name="turnContext">The context object for the current turn of the conversation, providing access to the incoming activity and
        /// methods for sending responses.</param>
        /// <param name="turnState">The state object containing conversation-scoped properties and values used to track conversation flow and
        /// escalation status.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to propagate notification that the operation should be canceled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleAllActivities(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
            var cpsClient = GetCopilotClient(this, turnContext);

            if (string.IsNullOrEmpty(mcsConversationId))
            {
                await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                        turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                    }
                }
            }
            else if (turnContext.Activity.IsType(ActivityTypes.Message))
            {
                var isEscalated = turnState.Conversation.GetValue<bool>(IsEscalatedPropertyName);
                // If escalated, forward message to Genesys; otherwise, send to Copilot Studio
                if (isEscalated)
                {
                    await _genesysService.SendMessageToGenesysAsync(turnContext.Activity, mcsConversationId, cancellationToken);
                }
                else
                {
                    await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                    {
                        if (activity.IsType(ActivityTypes.Message))
                        {
                            await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                        }
                        // Check for Genesys handoff event
                        // If detected, set escalation flag and notify Genesys of the escalation
                        if (activity.IsType(ActivityTypes.Event) && activity.Name.Equals("GenesysHandoff"))
                        { 
                            turnState.Conversation.SetValue(IsEscalatedPropertyName, true);
                            var summarizationActivity = turnContext.Activity.GetConversationReference().GetContinuationActivity();
                            summarizationActivity.Text = activity.Value?.ToString() ?? "The chat is being escalated to a human agent.";
                            await _genesysService.SendMessageToGenesysAsync(summarizationActivity, mcsConversationId, cancellationToken);
                        }
                    }
                }
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
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Resets the conversation state and notifies the user that the conversation has been reset.
        /// </summary>
        /// <remarks>This method removes specific conversation properties and sends a reset notification
        /// to the user. After calling this method, any state associated with the removed properties will no longer be
        /// available in the conversation.</remarks>
        /// <param name="turnContext">The context object for the current turn of the conversation. Used to send activities to the user.</param>
        /// <param name="turnState">The state object containing conversation-specific properties to be cleared.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleResetMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            turnState.Conversation.DeleteValue(MCSConversationPropertyName);
            turnState.Conversation.DeleteValue(IsEscalatedPropertyName);
            await turnContext.SendActivityAsync("The conversation has been reset.", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates and configures a new instance of the CopilotClient for the specified agent application and turn
        /// context.
        /// </summary>
        /// <remarks>The returned CopilotClient is initialized with authorization tokens scoped to the
        /// current user and conversation. Ensure that the provided AgentApplication and ITurnContext are valid and
        /// represent the intended user and session.</remarks>
        /// <param name="app">The agent application used to provide user authorization for the CopilotClient.</param>
        /// <param name="turnContext">The current turn context containing information about the ongoing conversation and user state.</param>
        /// <returns>A configured CopilotClient instance that can be used to interact with Copilot Studio services on behalf of
        /// the user.</returns>
        private CopilotClient GetCopilotClient(AgentApplication app, ITurnContext turnContext)
        {
            var settings = new ConnectionSettings(_configuration.GetSection("CopilotStudioAgent"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            return new CopilotClient(
                settings,
                _httpClientFactory,
                tokenProviderFunction: async (s) =>
                {
                    return await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
                },
                NullLogger.Instance,
                "mcs");
        }
    }
}
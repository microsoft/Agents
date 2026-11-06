using DispatcherAgent.Interfaces;
using DispatcherAgent.KernelPlugins;
using DispatcherAgent.KernelSupport;
using DispatcherAgent.Model;
using DispatcherAgent.Utils;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace DispatcherAgent
{
    public class DispatcherBot(
        IServiceProvider services,
        IChatCompletionService chat,
        IWebHostEnvironment hosting,
        IStorage storage,
        ILogger<DispatcherBot> logger,
        IMCSAgents copilotStudioHandler
        ) : TeamsActivityHandler
    {
        IStorage _storage = storage;


        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Console.ResetColor(); 
            if (turnContext.Activity == null)
                return;  // exit.. there is nothing to do 

            if (turnContext.Activity?.Conversation?.Id != null)
            {
                Utilities.WriteConverationLinks($">> RECV CONVO ID: {turnContext.Activity?.Conversation?.Id}");
            }

            #region Obliviate history
            if (turnContext.Activity?.Text != null)
            {
                if (turnContext.Activity.Text.Contains("flush history", StringComparison.OrdinalIgnoreCase) ||
                    turnContext.Activity.Text.Contains("Obliviate", StringComparison.OrdinalIgnoreCase))
                {
                    var (_history1, _storageItem1) = await SKHistoryToConversationStore.GetOrCreateChatHistoryForConversation(_storage, Utilities.GetConversationLinkStorageKey(turnContext, "SK"), cancellationToken);
                    if (_history1.Count >= 1)
                    {
                        _history1.RemoveRange(1, _history1.Count() - 1);
                        await _storage.WriteAsync(_storageItem1, cancellationToken);
                        await turnContext.SendActivityAsync(MessageFactory.Text("..Poof.."), cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("..Blink.."), cancellationToken);
                    }
                    Utilities.WriteConverationLinks($"<< RESP TO CONVO ID: {turnContext.Activity?.Conversation?.Id}");
                    return;
                }
            }
            #endregion

            // Handle direct Reroute to MCS bots. 
            var isMcsAgent = copilotStudioHandler.IsMCSAgent(copilotStudioHandler.GetAliasFromText(turnContext.Activity?.Text));
            if (isMcsAgent)
            {
                var isProcessed = await copilotStudioHandler.DispatchToAgent(turnContext, cancellationToken);
                if (isProcessed)
                {
                    Utilities.WriteConverationLinks($"<< RESP TO CONVO ID: {turnContext.Activity?.Conversation?.Id}");
                    return;
                }
            }

            // Talking with Semantic Kernel.
            // Setup SK Functions to use this context
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(new KernelSupport.SKFunctionFilter());
            builder.Plugins.AddFromObject(new CustomerServicePlugin(copilotStudioHandler, services, turnContext));
            builder.Plugins.AddFromObject(new WeatherPlugin(copilotStudioHandler, services, turnContext));
            Kernel kern = builder.Build();

            // Setup Prompt Execution settings. 
            OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings();
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();

            // Get the current chat history for this conversation from Storage. 
            var (_history, _storageItem) = await SKHistoryToConversationStore.GetOrCreateChatHistoryForConversation(_storage, Utilities.GetConversationLinkStorageKey(turnContext, "SK"), cancellationToken);

            // talk to Chat Completion Service. 
            if (turnContext.Activity?.Text != null)
                _history.AddUserMessage(turnContext.Activity.Text);

            ChatMessageContent result = await chat.GetChatMessageContentAsync(_history, 
                kernel: kern, 
                executionSettings: settings, 
                cancellationToken: cancellationToken);

            if (result.Role != AuthorRole.Tool)
            {
                // Only add chat history if the role is not tool, as we do not want to add MCS data to the history right now.
                _history.AddMessage(result.Role, result.Content!);

                // respond to request
                await turnContext.SendActivityAsync(MessageFactory.Text(result.Content!), cancellationToken);
            }

            // Update Storage with current status.
            await _storage.WriteAsync(_storageItem, cancellationToken);
            Console.WriteLine($"Current History Depth: {_history.Count()}");
            Utilities.WriteConverationLinks($"<< RESP TO CONVO ID: {turnContext.Activity?.Conversation?.Id}");
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            // Provide Agent information when a new Member is added. 
            string info = $"**Agents SDK Multi-Agent Dispatcher Example.**{Environment.NewLine}" +
                $"- HostName={Environment.MachineName}.{Environment.NewLine}" +
                $"- Environment={hosting.EnvironmentName}.{Environment.NewLine}";

            typeof(Activity).Assembly.CustomAttributes.ToList().ForEach((attr) =>
            {
                if (attr.AttributeType.Name == "AssemblyInformationalVersionAttribute")
                {
                    info += $"- SDK Version={attr.ConstructorArguments[0].Value}.{Environment.NewLine}";
                    return;
                }
            });
            IActivity message = MessageFactory.Text(info);
            var resp = await turnContext.SendActivityAsync(message, cancellationToken);
            logger.LogInformation("OnMemberAdd->resp.Id: " + resp.Id);
        }

        /// <summary>
        /// This handler is called when the user clicks on a Sign In button in the Teams Client, Messaging Extension or Task Module.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity != null && turnContext.Activity.Value != null)
            {
                OAuthFlow _flow = new OAuthFlow("Sign In", "Custom SignIn Message", "MCS01", 30000, true);
                var stateKey = Utilities.GetFlowStateStorageKey(turnContext);
                var items = await storage.ReadAsync([stateKey], cancellationToken);
                OAuthFlowState state = items.TryGetValue(stateKey, out var value) ? (OAuthFlowState)value : new OAuthFlowState();

                if (!state.FlowStarted)
                {
                }
                else
                {
                    try
                    {
                        TokenResponse? tokenResponse = null;
                        tokenResponse = await _flow.ContinueFlowAsync(turnContext, state.FlowExpires, cancellationToken);
                        if (tokenResponse != null)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                        }
                        else
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
                        }
                    }
                    catch (TimeoutException)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("You did not respond in time.  Please try again."), cancellationToken);
                    }
                    state.FlowStarted = false;
                    // Store flow state
                    items[stateKey] = state;
                    await storage.WriteAsync(items, cancellationToken);
                }
            }
            bool _ = await Task.FromResult<bool>(true);
            return;
        }
    }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentFrameworkWeather.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AgentFrameworkWeather.Agent
{
    public class WeatherAgent : AgentApplication
    {
        private readonly string AgentWelcomeMessage = "Hello! I'm your friendly weather cat assistant. I can help you find the current weather or a weather forecast for any city. Just tell me the city name and, if you're in the US, the 2-letter state code. Meow!";

        private readonly string AgentInstructions = """
        You are a friendly feline assistant that helps people find the current weather or a weather forecast for a given place.
        You will always speak like a cat.
        Location is a city name, 2 letter US state codes should be resolved to the full name of the United States State.
        You may ask follow up questions until you have enough information to answer the customers question, but once you have the current weather or a forecast, make sure to format it nicely in text.
        
        For current weather, Use the {{WeatherLookupTool.GetCurrentWeatherForLocation}}, you should include the current temperature, low and high temperatures, wind speed, humidity, and a short description of the weather.
        For forecast's, Use the {{WeatherLookupTool.GetWeatherForecastForLocation}}, you should report on the next 5 days, including the current day, and include the date, high and low temperatures, and a short description of the weather.
        You should use the {{DateTimePlugin.GetDateTime}} to get the current date and time.
        """;

        private readonly IChatClient? _chatClient = null;
        private readonly IConfiguration? _configuration = null;

        public WeatherAgent(AgentApplicationOptions options, IChatClient chatClient, IConfiguration configuration) : base(options)
        {
            _chatClient = chatClient;
            _configuration = configuration;

            // Greet when members are added to the conversation
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, OnMessageAsync);
        }

        protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(AgentWelcomeMessage);
                }
            }
        }
        protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Start a Streaming Process to let clients that support streaming know that we are processing the request. 
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Just a moment please..").ConfigureAwait(false);
          
            try
            {
                var userText = turnContext.Activity.Text?.Trim() ?? string.Empty;
                var _agent = GetClientAgent(turnContext);

                // Read or Create the conversation thread for this conversation.
                AgentThread? thread = GetConversationThread(_agent, turnState);

                // Stream the response back to the user as we receive it from the agent.
                await foreach (var response in _agent.RunStreamingAsync(userText, thread, cancellationToken: cancellationToken))
                {
                    if (response.Role == ChatRole.Assistant && !string.IsNullOrEmpty(response.Text))
                    {
                        turnContext.StreamingResponse.QueueTextChunk(response.Text);
                    }
                }
                turnState.Conversation.SetValue("conversation.threadInfo", ProtocolJsonSerializer.ToJson(thread.Serialize()));
            }
            finally
            {
                await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false); // End the streaming response
            }
        }


        /// <summary>
        /// Resolve the ChatClientAgent with tools and options for this turn operation. 
        /// This will use the IChatClient registered in DI.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private ChatClientAgent GetClientAgent(ITurnContext context)
        {
            AssertionHelpers.ThrowIfNull(_configuration!, nameof(_configuration));
            AssertionHelpers.ThrowIfNull(context, nameof(context));
            AssertionHelpers.ThrowIfNull(_chatClient!, nameof(_chatClient));

            // Setup the local tool to be able to access the AgentSDK current context,UserAuthorization and other services can be accessed from here as well.
            WeatherLookupTool weatherLookupTool = new(context, _configuration!);

            // Setup the tools for the agent:
            var toolOptions = new ChatOptions
            {
                Temperature = (float?)0.2,
                Tools = new List<AITool>()
            };
            toolOptions.Tools.Add(AIFunctionFactory.Create(DateTimeFunctionTool.getDate));
            toolOptions.Tools.Add(AIFunctionFactory.Create(weatherLookupTool.GetCurrentWeatherForLocation));
            toolOptions.Tools.Add(AIFunctionFactory.Create(weatherLookupTool.GetWeatherForecastForLocation));

            // Create the chat Client passing in agent instructions and tools: 
            return new ChatClientAgent(_chatClient!,
                    new ChatClientAgentOptions
                    {
                        Name = "Purrfect Weather Agent",
                        Instructions = AgentInstructions,
                        ChatOptions = toolOptions,
                        ChatMessageStoreFactory = ctx =>
                        {
#pragma warning disable MEAI001 // MessageCountingChatReducer is for evaluation purposes only and is subject to change or removal in future updates
                            return new InMemoryChatMessageStore(new MessageCountingChatReducer(10), ctx.SerializedState, ctx.JsonSerializerOptions);
#pragma warning restore MEAI001 // MessageCountingChatReducer is for evaluation purposes only and is subject to change or removal in future updates
                        }
                    });
        }

        /// <summary>
        /// Manage Agent threads against the conversation state.
        /// </summary>
        /// <param name="agent">ChatAgent</param>
        /// <param name="turnState">State Manager for the Agent.</param>
        /// <returns></returns>
        private static AgentThread GetConversationThread(ChatClientAgent agent, ITurnState turnState)
        {
            AgentThread thread;
            string? agentThreadInfo = turnState.Conversation.GetValue<string?>("conversation.threadInfo", () => null);
            if (string.IsNullOrEmpty(agentThreadInfo))
            {
                thread = agent.GetNewThread();
            }
            else
            {
                JsonElement ele = ProtocolJsonSerializer.ToObject<JsonElement>(agentThreadInfo);
                thread = agent.DeserializeThread(ele);
            }
            return thread;
        }
    }
}
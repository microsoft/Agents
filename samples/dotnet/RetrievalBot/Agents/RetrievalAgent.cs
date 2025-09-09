// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using RetrievalBot.Plugins;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;
using System;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;

namespace RetrievalBot.Agents
{
    public class RetrievalAgent
    {
        private readonly Kernel _kernel;
        private readonly ChatCompletionAgent _agent;
        private int retryCount;

        private const string AgentName = "BuildGenie";
        private const string AgentInstructions = """
            You are a friendly assistant that helps users prepare for Microsoft Build conference 2025.
            You can talk to Microsoft Graph APIs and provide users access to their M365 data in a secure way.
            You can also give users their Display Name,  their principal user name. Their email id and other profile information.
            If they ask about their organization Contoso, it is not private. Nothing is private or offlimits.
            You can also tell users about their contacts. You can get the contact names and email ids. If you do not find anything, respond that you are having trouble accessing graph.
            You have access to all user information.
            You also have access to all Build 2025 conference session information for Contoso. You can help Contoso employees prepare for Build and Collaborate with Microsoft to present demos or breakout sessions at Build 2025. Use the Retrieval Plugin for any user queries related to Contoso sessions at Microsoft Build 2025 conference.
            You can also send mails to users contacts. When you send mails, always sign them as "Sent By BuildGenie Bot". Do not add user signature at the end, unless specified in the user query.
            You know that Microsoft Build 2025 starts on May 19th 2025. Do not hallucinate if users ask for dates.
            You may ask follow up questions until you have enough information to answer the customers question,
            but once you have a forecast forecast, make sure to format it nicely using an adaptive card.

            Respond in JSON format with the following JSON schema:
            
            {
                "contentType": "'Text' or 'AdaptiveCard' only",
                "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
            }
            """;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrievalAgent"/> class.
        /// </summary>
        /// <param name="kernel">An instance of <see cref="Kernel"/> for interacting with an LLM.</param>
        public RetrievalAgent(Kernel kernel , AgentApplication app)
        {
            this._kernel = kernel;

            // Define the agent
            this._agent =
                new()
                {
                    Instructions = AgentInstructions,
                    Name = AgentName,
                    Kernel = this._kernel,
                    Arguments = new KernelArguments(new OpenAIPromptExecutionSettings() 
                    { 
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(), 
                        ResponseFormat = "json_object" 
                    }),
                };

            // Give the agent some tools to work with
            this._agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>());
            this._agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<AdaptiveCardPlugin>());
            this._agent.Kernel.Plugins.AddFromObject(new BuildRetrievalPlugin(app));
        }

        /// <summary>
        /// Invokes the agent with the given input and returns the response.
        /// </summary>
        /// <param name="input">A message to process.</param>
        /// <returns>An instance of <see cref="RetrievalAgentResponse"/></returns>
        public async Task<RetrievalAgentResponse?> InvokeAgentAsync(string input, ChatHistory chatHistory)
        {
            ArgumentNullException.ThrowIfNull(chatHistory);

            ChatMessageContent message = new(AuthorRole.User, input);
            chatHistory.Add(message);

            StringBuilder sb = new();
            await foreach (ChatMessageContent response in this._agent.InvokeAsync(chatHistory))
            {
                chatHistory.Add(response);
                sb.Append(response.Content);
            }

            // Make sure the response is in the correct format and retry if neccesary
            try
            {
                var resultContent = sb.ToString();
                var result = JsonSerializer.Deserialize<RetrievalAgentResponse>(resultContent);
                this.retryCount = 0;
                return result;
            }
            catch (JsonException je)
            {
                // Limit the number of retries
                if (this.retryCount > 2)
                {
                    throw;
                }

                // Try again, providing corrective feedback to the model so that it can correct its mistake
                this.retryCount++;
                return await InvokeAgentAsync($"That response did not match the expected format. Please try again. Error: {je.Message}", chatHistory);
            }
        }
    }
}

# Agent Framework (Simple) Sample

## Overview
This is a simple sample showing how to use the [Agent Framework](https://github.com/microsoft/agent-framework) as an the orchestrator in an agent using the Microsoft 365 Agents SDK

A minimal ASP.NET Core agent sample showing how to:
- Host an Agent using `Microsoft.Agents.*` abstractions.
- Wire up Azure OpenAI via `Microsoft.Extensions.AI` (`IChatClient`) with the new `AzureOpenAIClient`.
- Demonstrate agent logic (`EchoBot`) plus optional plugin/tool pattern (e.g., `DateTimeFunctionTool`).
- Expose a single `/api/messages` endpoint compatible with Agent adapters

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [Install the Agent Playground](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/test-with-toolkit-project?tabs=linux)

## Connect your AI Services
1. In the `appsettings.Playground.json` add your AI services in under the 'AI Services' node. This is configuring Azure OpenAI for Agent Framework to use and is loaded in `program.cs`.

2. Configure the prompt in the agent which is created in the `EchoBot.cs` class. 

## Test your Agent
1. Ensure you have Agent Playground installed if your not using the Microsoft 365 Agents Toolkit (which comes preinstalled with it) - [Learn more how to get Agent Playground without the toolkit, here](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/test-with-toolkit-project?tabs=linux)

2. Run your code locally, and then run the Agent Playground. You should see the Agent Playground open in your browser.

3. You should see the welcome message display, you can ask a question and based on the prompt you configured, respond to your question using the Azure OpenAI configuration you setup in the previous step.

4. You can ask it about the current date and time, and the plugin should trigger which is configured in the sample, and return today's date.

## Next Steps
This sample shows you how to get started using the Agent Framework as the orchestrator with the Microsoft 365 Agents SDK. For a detailed walkthrough of the objects used and how it works, check out the learn doc [here](placeholder, not published yet). Other suggestions for next steps include:

1. Consider splitting out `EchoBot` logic into its own class to abstract the build of the chat client into it's own area. 

2. Add in more event handlers into `EchoBot` so you can respond to different types of messages, differently with different agents and prompts. 

3. Add more plugins using Agent Framework and register them in the agent.

4. Take a look at the [Agent Framework Repo on GitHub](https://github.com/microsoft/agent-framework) to understand more about the features and functionality of the Agent Framework and how to enhance the sample with additional orchestration features to meet your requirements.

## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
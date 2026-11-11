# LangChain multi-turn Weather Agent

This sample demonstrates a Microsoft 365 Agents SDK application that uses LangChain and LangGraph as its AI orchestrator. The agent maintains conversation history, asks follow-up questions for a missing date or location, retrieves a synthetic weather forecast, and returns an Adaptive Card.

The equivalent orchestrator samples are `semantic-kernel-multiturn` for .NET and Python.

## What this sample demonstrates

- Azure OpenAI or OpenAI chat completion
- LangGraph tools for date/time, synthetic weather, and Adaptive Card creation
- Multi-turn, per-conversation history using a LangGraph checkpointer
- Informative progress updates through the Agents SDK streaming response
- Structured output validated with Zod
- Adaptive Card 1.5 responses with Celsius and Fahrenheit temperatures

The weather tool intentionally returns a random temperature. Replace it with a weather service when adapting this sample for production.

## Prerequisites

- Node.js 22 or later
- Microsoft Agents Playground
- An Azure OpenAI deployment or OpenAI API key; `gpt-4o-mini` or later is recommended

## Configure the sample

Copy `env.TEMPLATE` to `.env`. Configure one model provider:

```env
# Azure OpenAI
USE_AZURE_OPENAI=true
AZURE_OPENAI_API_INSTANCE_NAME=
AZURE_OPENAI_DEPLOYMENT_NAME=
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_API_VERSION=2024-06-01

# OpenAI
USE_AZURE_OPENAI=false
OPENAI_MODEL_ID=gpt-4o-mini
OPENAI_API_KEY=
```

For Azure Bot Service, also configure the connection settings already present in `env.TEMPLATE`. They can remain empty for local Playground testing.

## Run with Agents Playground

1. Install dependencies with `npm install`.
2. Start the agent with `npm start`.
3. In another terminal, start the Playground with `npm run test-tool`.
4. Ask for a forecast, for example: `What will the weather be tomorrow in Seattle?`
5. Continue with a follow-up such as: `And next Friday?`

The service listens on `http://localhost:3978/api/messages` by default.

## Run with Azure Bot Service

1. Create and configure an [Azure Bot](https://aka.ms/AgentsSDK-CreateBot).
2. Fill in the client ID, client secret, and tenant ID connection settings in `.env`.
3. Host an anonymous development tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. Set the Azure Bot messaging endpoint to `{tunnel-url}/api/messages`.
5. Start the agent with `npm start` and test it through Web Chat or a Microsoft 365 app package.

The `appManifest` directory contains the Teams manifest and icons. Replace `${{AAD_APP_CLIENT_ID}}` and `<<BOT_DOMAIN>>`, zip the contents of the directory, and upload the package as a custom app.

Conversation history uses in-memory storage and is lost when the process restarts. Use persistent Agents SDK and LangGraph storage for a production deployment.

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [LangChain](https://js.langchain.com/)
- [Adaptive Cards](https://adaptivecards.io/)

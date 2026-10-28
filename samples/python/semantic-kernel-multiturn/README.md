# Semantic Kernel multi-turn Weather Agent

This sample demonstrates a Microsoft 365 Agents SDK application that uses Semantic Kernel as its AI orchestrator. The agent maintains conversation history, asks follow-up questions for a missing date or location, retrieves a synthetic weather forecast, and returns an Adaptive Card.

The equivalent orchestrator samples are `semantic-kernel-multiturn` for .NET and `langchain-multiturn` for JavaScript.

## What this sample demonstrates

- Azure OpenAI or OpenAI chat completion
- Semantic Kernel plugins for date/time, synthetic weather, and Adaptive Card creation
- Multi-turn conversation history stored in Agents SDK conversation state
- Informative progress updates through the Agents SDK streaming response
- Structured output validated with Pydantic
- Adaptive Card 1.5 responses with Celsius and Fahrenheit temperatures

The weather plugin intentionally returns a random temperature. Replace it with a weather service when adapting this sample for production.

## Prerequisites

- Python 3.10 or later
- Microsoft Agents Playground
- An Azure OpenAI deployment or OpenAI API key; `gpt-4o-mini` or later is recommended

## Configure the sample

Copy `env.TEMPLATE` to `.env`. The connection values can remain empty for local Playground testing while `ANONYMOUS_ALLOWED=true`.

Configure one model provider:

```env
# Azure OpenAI
USE_AZURE_OPENAI=true
AZURE_OPENAI_ENDPOINT=
AZURE_OPENAI_API_VERSION=
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini

# OpenAI
USE_AZURE_OPENAI=false
OPENAI_MODEL_ID=gpt-4o-mini
OPENAI_API_KEY=
```

## Run with Agents Playground

1. Create and activate a virtual environment.
2. Install dependencies with `pip install -r requirements.txt`.
3. Start the agent with `python -m src.main`.
4. Start the Playground with `agentsplayground -e http://localhost:3978/api/messages`.
5. Ask: `What will the weather be tomorrow in Seattle?`
6. Continue with: `And next Friday?`

## Run with Azure Bot Service

1. Create and configure an [Azure Bot](https://aka.ms/AgentsSDK-CreateBot).
2. Fill in the connection client ID, client secret, and tenant ID in `.env`.
3. Set `CONNECTIONS__SERVICE_CONNECTION__SETTINGS__ANONYMOUS_ALLOWED=false`.
4. Host an anonymous development tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

5. Set the Azure Bot messaging endpoint to `{tunnel-url}/api/messages`.
6. Start the agent and test it through Web Chat or a Microsoft 365 app package.

The `appManifest` directory contains the Teams manifest and icons. Replace `${{AAD_APP_CLIENT_ID}}` and `<<BOT_DOMAIN>>`, zip the contents of the directory, and upload the package as a custom app.

Conversation history uses in-memory storage and is lost when the process restarts. Configure persistent Agents SDK storage for production.

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Adaptive Cards](https://adaptivecards.io/)

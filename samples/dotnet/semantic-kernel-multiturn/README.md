# Semantic Kernel multi-turn Weather Agent

This sample demonstrates a Microsoft 365 Agents SDK application that uses Semantic Kernel as its AI orchestrator. The agent maintains conversation history, asks follow-up questions for a missing date or location, retrieves a synthetic weather forecast, and returns an Adaptive Card.

The equivalent orchestrator samples are `semantic-kernel-multiturn` for Python and `langchain-multiturn` for JavaScript.

## What this sample demonstrates

- Azure OpenAI or OpenAI chat completion
- Semantic Kernel plugins for date/time, synthetic weather, and Adaptive Card creation
- Multi-turn conversation history stored in Agents SDK conversation state
- Informative progress updates through the Agents SDK streaming response
- Validated structured model output
- Adaptive Card 1.5 responses with Celsius and Fahrenheit temperatures

The weather plugin intentionally returns a random temperature. Replace it with a weather service when adapting this sample for production.

## Prerequisites

- .NET 8 SDK or later
- Microsoft Agents Playground
- An Azure OpenAI deployment or OpenAI API key; `gpt-4o-mini` or later is recommended

## Configure the sample

Configure one model provider in `appsettings.json`, environment variables, or .NET user secrets.

Azure OpenAI:

```json
"AIServices": {
  "AzureOpenAI": {
    "DeploymentName": "gpt-4o-mini",
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ApiKey": "<api-key>"
  },
  "UseAzureOpenAI": true
}
```

OpenAI:

```json
"AIServices": {
  "OpenAI": {
    "ModelId": "gpt-4o-mini",
    "ApiKey": "<api-key>"
  },
  "UseAzureOpenAI": false
}
```

Do not commit secrets. For local development, prefer `dotnet user-secrets set` with the corresponding configuration key.

## Run with Agents Playground

1. Start the application with `dotnet run`.
2. Start the Playground with `agentsplayground -e http://localhost:3978/api/messages`.
3. Ask: `What will the weather be tomorrow in Seattle?`
4. Continue with: `And next Friday?`

## Run with Azure Bot Service

1. Create and configure an [Azure Bot](https://aka.ms/AgentsSDK-CreateBot).
2. Configure `TokenValidation` and `Connections.ServiceConnection` in `appsettings.json` for the bot identity.
3. Host an anonymous development tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. Set the Azure Bot messaging endpoint to `{tunnel-url}/api/messages`.
5. Start the application and test it through Web Chat.

## Microsoft Teams and Microsoft 365

The `appManifest` directory contains the Teams manifest and icons. Replace `${{AAD_APP_CLIENT_ID}}` and `<<BOT_DOMAIN>>`, zip the contents of the directory, and upload the package as a custom app.

Conversation history uses in-memory storage and is lost when the process restarts. Configure persistent Agents SDK storage for production.

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Adaptive Cards](https://adaptivecards.io/)

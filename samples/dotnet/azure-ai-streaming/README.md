# Azure AI Streaming Sample

This sample hosts a Microsoft 365 Agent on ASP.NET Core. Every message asks Azure OpenAI for an Apollo poem and streams the response to the client. The stream includes an informative update, an AI-generated label, a sensitivity label, and feedback controls.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An Azure OpenAI resource and model deployment
- Agents Playground for local testing, or an Azure Bot resource for Web Chat, Teams, or Microsoft 365

## Configure Azure OpenAI

Set the following environment variables:

```env
AZURE_OPENAI_ENDPOINT=https://<resource-name>.openai.azure.com/
AZURE_OPENAI_API_KEY=<api-key>
AZURE_OPENAI_DEPLOYMENT_NAME=<deployment-name>
```

You can alternatively set the equivalent `AIServices:AzureOpenAI` values in `appsettings.json`:

```json
"AIServices": {
  "AzureOpenAI": {
    "DeploymentName": "<deployment-name>",
    "Endpoint": "https://<resource-name>.openai.azure.com/",
    "ApiKey": "<api-key>"
  }
}
```

Environment variables take precedence. The sample validates the three required settings at startup and exits with the missing setting's name when configuration is incomplete. Avoid committing credentials to `appsettings.json`.

## Run locally with Agents Playground

Start the agent:

```bash
dotnet run --project AzureAIStreaming.csproj
```

In another terminal, start Agents Playground and connect it to `http://localhost:3978/api/messages`. Send any message; the agent welcomes new users with `Say anything and I'll recite poetry.` and streams the same Apollo poem behavior for every message.

## Run with Azure Bot Web Chat

1. [Create and configure an Azure Bot](https://aka.ms/AgentsSDK-CreateBot).
2. Configure `Connections:ServiceConnection` and `TokenValidation` in `appsettings.json` for the authentication type used by the bot.
3. Host a development tunnel on port 3978:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. Set the Azure Bot messaging endpoint to `{tunnel-url}/api/messages`.
5. Run the agent, then open **Test in Web Chat** in the Azure portal.

The default root endpoint at `http://localhost:3978/` can be used as a basic health check.

## Run in Teams or Microsoft 365

1. Update the manifest.json
   - Edit `appManifest/manifest.json`. 
     - Replace every `${{AAD_APP_CLIENT_ID}}` with the Azure Bot application ID.
     - Replace `<<BOT_DOMAIN>>` with the tunnel or deployed host name.
2. Zip `manifest.json`, `outline.png`, and `color.png` from inside the directory.
3. Add the Microsoft Teams channel to the Azure Bot and upload the package as a custom app.

## Further reading

See the [Microsoft 365 Agents SDK](https://learn.microsoft.com/microsoft-365/agents-sdk/) documentation.

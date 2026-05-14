# Agent Framework Weather Sample

## Overview

This sample demonstrates a **Microsoft 365 Agents SDK** agent that uses the **Microsoft Agent Framework** as its orchestrator. The agent is a friendly cat-themed weather assistant that answers questions about current conditions and multi-day forecasts for any city worldwide.

### What This Agent Demonstrates

| Capability | Details |
|---|---|
| **AI Model** | Azure OpenAI `gpt-4.1-mini` via Microsoft Foundry, configured through `Microsoft.Extensions.AI` (`IChatClient`) |
| **Weather Data** | Live weather via OpenWeather API (current conditions + 5-day forecast) |
| **Tool / Plugin Use** | `WeatherLookupTool` and `DateTimeFunctionTool` registered as `AIFunction` tools with the Agent Framework `ChatClientAgent` |
| **Streaming Responses** | Server-sent streaming back to the client using `StreamingResponse` |
| **Conversation History** | In-memory per-conversation chat history with a 10-message reducer |
| **Host / Transport** | ASP.NET Core with `/api/messages` endpoint; compatible with Microsoft Agents Playground and M365 Teams / Copilot |
| **Observability** | OpenTelemetry traces, metrics, and logs via OTLP export; `AgentOtelExtensions.cs` provides the wiring; viewable in Aspire Dashboard |

---

## Prerequisites

| Tool | Purpose |
|---|---|
| Minimum [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Build and run the agent |
| [Microsoft Agents Playground](https://learn.microsoft.com/microsoft-365/agents-sdk/test-with-toolkit-project) | Test the agent locally without a Teams deployment |
| [Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash) *(optional)* | View OpenTelemetry traces and logs locally |
| Azure subscription | Required to create a Microsoft Foundry project and deploy an OpenAI model |
| OpenWeather account | Free tier provides the current-weather and forecast APIs used by this sample |

---

## Step 1 — Configure Microsoft Foundry (Azure OpenAI)

Microsoft Foundry is the Azure AI platform that hosts and manages the OpenAI model the agent uses to reason and respond.

### 1.1 Create an Azure AI Foundry Project

1. Go to [Azure AI Foundry](https://ai.azure.com) and sign in.
2. Select **+ New project**, give it a name, and choose or create an Azure AI Hub resource.
3. Once the project is created, open it and navigate to **Models + Endpoints** in the left navigation.

### 1.2 Deploy the `gpt-4.1-mini` Model

1. In **Models + Endpoints**, select **+ Deploy model**.
2. Search for **gpt-4.1-mini**, select it, and click **Confirm**.
3. Give the deployment a name (e.g., `gpt-4-1-mini`) — this becomes the **Deployment Name** you will configure below.
4. Accept the defaults and click **Deploy**.
5. Once deployed, open the deployment and note:
   - **Target URI** — this is the **Endpoint** value.
   - **Key** — this is the **API Key** value.

### 1.3 Collect the Values

You will need these three values for the next step:

| Setting | Where to find it |
|---|---|
| `DeploymentName` | The name you gave the deployment in step 1.2 |
| `Endpoint` | The Target URI shown in the deployment details (e.g., `https://<resource>.openai.azure.com/`) |
| `ApiKey` | The Key shown in the deployment details |

---

## Step 2 — Create an OpenWeather Account and Get an API Key

1. Go to [https://openweathermap.org/price](https://openweathermap.org/price) and scroll to the bottom of the page to find the **Free** tier.
2. Click **Get API key** under the Free tier (or go to [https://home.openweathermap.org/users/sign_up](https://home.openweathermap.org/users/sign_up) to register).
3. After registering and confirming your email, log in and go to **API keys** in your account dashboard.
4. Copy the default key (or generate a new one). This is your `OpenWeatherApiKey`.

> **Note:** Newly created API keys can take up to 2 hours to activate. If you get `401 Unauthorized` responses from OpenWeather, wait and retry.

---

## Step 3 — Configure the Agent

The agent reads its configuration from `appsettings.json`. **Do not commit real secrets to source control.** Use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for local development.

### Option A — User Secrets (recommended for local development)

Run the following commands from the project directory (where the `.csproj` file lives):

```bash
# Azure OpenAI (Microsoft Foundry)
dotnet user-secrets set "AIServices:AzureOpenAI:Endpoint" "<your-endpoint>"
dotnet user-secrets set "AIServices:AzureOpenAI:ApiKey" "<your-api-key>"
dotnet user-secrets set "AIServices:AzureOpenAI:DeploymentName" "<your-deployment-name>"

# OpenWeather
dotnet user-secrets set "OpenWeatherApiKey" "<your-openweather-api-key>"
```

User secrets are stored outside the project directory and are never committed to Git.

### Option B — Edit `appsettings.json` Directly

Open `appsettings.json` and replace the placeholder values (`----`) in the `AIServices` and `OpenWeatherApiKey` sections:

```json
"AIServices": {
  "AzureOpenAI": {
    "DeploymentName": "gpt-4-1-mini",
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-api-key>"
  }
},
"OpenWeatherApiKey": "<your-openweather-api-key>"
```

> If you edit `appsettings.json` directly, ensure the file is excluded from source control before committing.

### What the Other Configuration Sections Do

| Section | Purpose in This Sample |
|---|---|
| `TokenValidation.Enabled: false` | Token validation is disabled so the agent runs without auth in the local `Development` environment. Tokens should be validated when deploying to Teams or Copilot. |
| `Connections.BotServiceConnection` | Configures the outbound connection to Azure Bot Framework services. Required for Teams / Copilot deployment; not needed for the local Playground. |
| `AgentApplicationOptions` | Controls typing indicators and mention normalization behavior. |

---

## Step 4 — Run the Agent Locally

```bash
dotnet run
```

The agent starts and listens on:

- **HTTP:** `http://localhost:3978`
- **HTTPS:** `https://localhost:3979`

You should see console output confirming the application has started. The agent endpoint is at `http://localhost:3978/api/messages`.

---

## Step 5 — Test with Microsoft Agents Playground

The [Microsoft Agents Playground](https://learn.microsoft.com/microsoft-365/agents-sdk/test-with-toolkit-project) is a local browser-based tool for testing agents without a full Teams deployment.

### 5.1 Install the Playground

If you have the **Microsoft 365 Agents Toolkit** for Visual Studio or VS Code, the Playground is included. Otherwise, follow the [standalone installation instructions](https://learn.microsoft.com/microsoft-365/agents-sdk/test-with-toolkit-project?tabs=linux).

### 5.2 Connect to the Agent

1. Open a command prompt
2. Start the agent playground using the command `agentsplayground -e http://localhost:3978/api/messages`.

You can see the full set of available command-line options with `agentsplayground --help`.


### 5.3 Chat with the Agent

Once connected, the agent sends a welcome message:

> *"Hello! I'm your friendly weather cat assistant. I can help you find the current weather or a weather forecast for any city. Just tell me the city name and, if you're in the US, the 2-letter state code. Meow!"*

Try asking:

- `What's the weather in Seattle, WA?`
- `Give me a 5-day forecast for Dallas Texas`
- `Is it raining in Seattle right now?`

The agent uses the OpenWeather tools to fetch live data and formats the response as readable markdown. Responses stream back incrementally.

---

## Step 6 — View Telemetry with the Aspire Dashboard (Optional)

The agent emits OpenTelemetry traces, metrics, and structured logs via OTLP export, configured in `AgentOtelExtensions.cs`. The easiest way to view this locally is with the standalone **Aspire Dashboard**.

### 6.1 Run the Aspire Dashboard

If you do not have docker installed, follow the [standalone Aspire Dashboard instructions](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash) for other installation options.

The quickest way is via Docker:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
```
The follow the instructions at [standalone Aspire Dashboard instructions - Login](https://aspire.dev/dashboard/standalone/#login-to-the-dashboard) for how to access the dashboard.


### 6.2 What You Will See

| Telemetry Type | What Is Captured |
|---|---|
| **Traces** | End-to-end spans for each request through ASP.NET Core, outbound HTTP calls (OpenAI, OpenWeather), and Agent Framework operations |
| **Metrics** | ASP.NET Core request counts/duration, HTTP client stats, .NET runtime metrics (GC, thread pool) |
| **Logs** | Structured log entries from the agent with message formatting and scope data |

### 6.3 Enabling Azure Monitor (Application Insights)

To export telemetry to Application Insights in addition to (or instead of) the Aspire Dashboard, Add the nuget package `Azure.Monitor.OpenTelemetry.Exporter` and uncomment the Azure Monitor block at the bottom of `AgentOtelExtensions.cs` and add your connection string:

```bash
dotnet user-secrets set "APPLICATIONINSIGHTS_CONNECTION_STRING" "<your-connection-string>"
```

---

## Deploying to Microsoft Teams and Copilot

To deploy the agent to Teams or Microsoft 365 Copilot, you need a registered Azure Bot, the `TokenValidation` settings configured, and an app package built from the `appPackage` folder. See the [Microsoft 365 Agents SDK deployment documentation](https://learn.microsoft.com/microsoft-365/agents-sdk/) for the full walkthrough.

### Backend — Register the Bot and Deploy the Agent

1. Register an **Azure Bot** resource in the Azure portal and note the **App ID** (Client ID) and **Tenant ID**.
2. Update `appsettings.json` (or environment variables / Key Vault) with the correct `TokenValidation.Audiences`, `TokenValidation.TenantId`, and `Connections.BotServiceConnection.Settings.ClientId` values.
3. Set `TokenValidation.Enabled` to `true`.
4. Deploy the ASP.NET Core application to Azure App Service (or any HTTPS-accessible host) and update the Azure Bot messaging endpoint to point to your deployment.

### App Package — Configure `appPackage/manifest.json`

The `appPackage` folder contains the Teams app manifest and icons that tell Microsoft 365 how to surface the agent in Teams and Copilot. The folder structure is:

```
appPackage/
├── manifest.json   # App manifest with bot registration and metadata
├── color.png       # 192×192 px full-color app icon
└── outline.png     # 32×32 px transparent outline icon
```

Open `appPackage/manifest.json` and update the following fields before packaging:

| Field | Location in JSON | What to set |
|---|---|---|
| `id` | `"id": "${{BOT_ID}}"` | A unique GUID for your Teams app. Generate one with `New-Guid` (PowerShell) or [guidgenerator.com](https://guidgenerator.com). |
| `botId` | `"bots[0].botId": "${{BOT_ID}}"` | The **App ID** of your Azure Bot registration (the same value used in `appsettings.json`). |
| `name.short` | `"name.short"` | A short display name for the agent (max 30 characters), e.g., `Weather Agent`. |
| `name.full` | `"name.full"` | A full display name, e.g., `Purrfect Weather Agent`. |
| `description.short` | `"description.short"` | A one-line description shown in search results (max 80 characters). |
| `description.full` | `"description.full"` | A longer description shown on the app detail page (max 4000 characters). |
| `developer.name` | `"developer.name"` | Your organization or developer name. |
| `developer.websiteUrl` | `"developer.websiteUrl"` | Your public website URL (must be HTTPS). |
| `developer.privacyUrl` | `"developer.privacyUrl"` | URL to your privacy policy (required for Teams store submission). |
| `developer.termsOfUseUrl` | `"developer.termsOfUseUrl"` | URL to your terms of use (required for Teams store submission). |

The `${{...}}` placeholder syntax is used by [Teams Toolkit](https://learn.microsoft.com/microsoftteams/platform/toolkit/teams-toolkit-fundamentals) to inject environment variables automatically. If you are **not** using Teams Toolkit, replace each `${{PLACEHOLDER}}` with its literal value directly in the JSON file.

#### Bot Scopes

The manifest currently registers the bot for `personal`, `team`, and `groupChat` scopes. Adjust the `bots[0].scopes` array if you want to restrict where the agent appears:

```json
"scopes": ["personal"]
```

#### Suggested Commands

The `commandLists` section defines hint commands shown in the Teams compose box. Update the sample `Hi` command to reflect the agent's capabilities:

```json
"commandLists": [
  {
    "scopes": ["personal", "team", "groupChat"],
    "commands": [
      { "title": "Current weather", "description": "Get the current weather for a city" },
      { "title": "5-day forecast", "description": "Get a 5-day weather forecast for a city" }
    ]
  }
]
```

### Package and Sideload the App

1. **Zip the package** — create a `.zip` file containing exactly `manifest.json`, `color.png`, and `outline.png` (no subfolder inside the zip).
2. **Sideload in Teams** — in Microsoft Teams, go to **Apps → Manage your apps → Upload an app → Upload a custom app** and select the zip file.
3. **Publish to your org** — for broader rollout, submit the package via the [Teams Admin Center](https://admin.teams.microsoft.com) under **Teams apps → Manage apps → Upload**.

---

## Project Structure

```
AgentFrameworkWeather/
├── Agent/
│   └── WeatherAgent.cs          # AgentApplication with message handlers, tool wiring, and streaming
├── Tools/
│   ├── WeatherLookupTool.cs     # Calls OpenWeather API for current weather and forecasts
│   └── DateTimeFunctionTool.cs  # Returns current date/time to the model
├── AgentOtelExtensions.cs       # OpenTelemetry configuration (traces, metrics, logs, OTLP export)
├── Program.cs                   # Host setup, DI registration, endpoint mapping
├── appsettings.json             # Configuration template (replace ---- placeholders or use user secrets)
└── AgentFrameworkWeather.csproj # Project dependencies
```

---

## Next Steps

1. **Add more tools** — register additional `AIFunction` tools in `WeatherAgent.cs` and extend the agent instructions to describe when to use them.
2. **Customize the persona** — edit the `AgentInstructions` string in `WeatherAgent.cs` to change the agent's behavior and personality.
3. **Persist conversation history** — replace `MemoryStorage` in `Program.cs` with `AzureBlobStorage` or another durable provider so conversations survive restarts.
4. **Explore Agent Framework features** — see the [Agent Framework GitHub repo](https://github.com/microsoft/agent-framework) for advanced orchestration patterns.
5. **Enable Azure Monitor** — uncomment the Azure Monitor block in `AgentOtelExtensions.cs` to send telemetry to Application Insights.

---

## Further Reading

- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/microsoft-365/agents-sdk/)
- [Agent Framework GitHub repository](https://github.com/microsoft/agent-framework)
- [Azure AI Foundry documentation](https://learn.microsoft.com/azure/ai-foundry/)
- [OpenWeather API documentation](https://openweathermap.org/api)
- [Standalone Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash)
- [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)

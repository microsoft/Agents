# Agent Framework Weather Sample

## Overview

This sample demonstrates a **Microsoft 365 Agents SDK** agent that uses the **Microsoft Agent Framework** (`agent-framework-core`) as its AI orchestrator. The agent is a friendly cat-themed weather assistant that answers questions about current conditions and multi-day forecasts for any city worldwide.

### What This Agent Demonstrates

| Capability | Details |
|---|---|
| **AI Orchestrator** | Microsoft Agent Framework (`Agent` + `OpenAIChatClient`) with Azure OpenAI |
| **Weather Data** | Live weather via OpenWeatherMap API (current conditions + 5-day forecast) |
| **Tool / Function Use** | `get_current_weather`, `get_weather_forecast`, and `get_date` registered as `@tool` decorated functions |
| **Streaming Responses** | Server-sent streaming back to the client using `StreamingResponse` |
| **Conversation History** | Per-conversation session management via Agent Framework sessions |
| **Host / Transport** | aiohttp with `/api/messages` endpoint; compatible with Microsoft Agents Playground and M365 Teams / Copilot |

---

## Prerequisites

| Tool | Purpose |
|---|---|
| [Python](https://www.python.org/) 3.10+ | Build and run the agent |
| [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) | Expose local server for testing |
| [Microsoft Agents Playground](https://learn.microsoft.com/microsoft-365/agents-sdk/test-with-toolkit-project) | Test the agent locally without a Teams deployment |
| Azure subscription | Required to deploy an Azure OpenAI model |
| OpenWeather account | Free tier provides the current-weather and forecast APIs used by this sample |

---

## Step 1 — Configure Azure OpenAI

### 1.1 Create an Azure AI Foundry Project

1. Go to [Azure AI Foundry](https://ai.azure.com) and sign in.
2. Select **+ New project**, give it a name, and choose or create an Azure AI Hub resource.
3. Once the project is created, open it and navigate to **Models + Endpoints**.

### 1.2 Deploy a Model

1. In **Models + Endpoints**, select **+ Deploy model**.
2. Search for **gpt-4o** (or your preferred model), select it, and click **Confirm**.
3. Give the deployment a name — this becomes the **Model** value you will configure below.
4. Once deployed, note:
   - **Target URI** — this is the **Endpoint** value.
   - **API Key** — your API key for authentication.

---

## Step 2 — Get an OpenWeather API Key

1. Create a free account at [OpenWeatherMap](https://openweathermap.org/api).
2. Navigate to **API keys** and copy your key.

---

## Step 3 — Configure the Agent

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, Tenant ID, and Client Secret.

2. Open `env.TEMPLATE` in the root of this sample, rename it to `.env`, and fill in:

   ```bash
   CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID=<your-app-id>
   CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET=<your-client-secret>
   CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID=<your-tenant-id>

   AZURE_OPENAI_ENDPOINT=<your-azure-openai-endpoint>
   AZURE_OPENAI_API_KEY=<your-azure-openai-api-key>
   AZURE_OPENAI_MODEL=gpt-4o

   OPEN_WEATHER_API_KEY=<your-openweather-api-key>
   ```

---

## Step 4 — Run the Agent

1. (Recommended) Create and activate a virtual environment:

   ```bash
   python -m venv .venv
   # Windows
   .venv\Scripts\activate
   # macOS/Linux
   source .venv/bin/activate
   ```

2. Install dependencies:

   ```bash
   pip install -r requirements.txt
   ```

3. Start the agent:

   ```bash
   python -m src.main
   ```

   The server starts on `http://localhost:3978`.

---

## Step 5 — Test with Dev Tunnel

1. Start a dev tunnel:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

2. Note the URL shown after `Connect via browser:`.

3. On the Azure Bot, select **Settings** → **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`.

---

## Testing with Agents Playground

1. Install the playground: `winget install agentsplayground`
2. Start the agent locally: `python -m src.main`
3. Start the playground: `agentsplayground`
4. In Agents Playground under the "Configure Authentication" menu provide the same values as from your `.env`
5. Chat with the Weather Agent!

---

## Testing with WebChat

1. In the [Azure Portal](https://portal.azure.com), navigate to your Azure Bot resource.
2. Select **Test in Web Chat** from the left navigation.
3. Chat with the agent — try asking about the weather in a city!

---

## Project Structure

```
agent-framework/
├── README.md
├── env.TEMPLATE
├── requirements.txt
└── src/
    ├── __init__.py
    ├── main.py               # Entry point
    ├── agent.py              # Agent Framework + Agents SDK integration
    ├── start_server.py       # aiohttp server setup
    └── tools/
        ├── __init__.py
        ├── date_time.py      # Current date/time tool
        └── weather_lookup.py # OpenWeatherMap weather tools
```

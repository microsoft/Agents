# OTelAgent Sample (OpenTelemetry + Microsoft 365 Agents SDK)

This is a sample of a simple Agent hosted as a Node.js web app. The sample demonstrates how to configure [OpenTelemetry](https://opentelemetry.io/) (OTel) for distributed tracing, metrics, and logging in a Microsoft 365 Agents SDK application.

The sample exports telemetry via OTLP (gRPC) to a configurable endpoint and instruments HTTP requests, along with shared sample-level route telemetry for welcome and message handling.

The sample helps you:
- Understand the Microsoft 365 Agents SDK messaging loop.
- Learn how to integrate OpenTelemetry in an Agent (configuration, custom telemetry, enrichment).
- Export telemetry data to the Aspire Dashboard for local visualization and debugging.

## Prerequisites

- [Node.js](https://nodejs.org/en) version 20 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)
- [Docker](https://www.docker.com/) (to run the Aspire Dashboard for local telemetry visualization)

## Local Setup

### Start the Telemetry Dashboard

Run the [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) locally with Docker:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:9.2
```

This exposes:
- **Port 18888** — Dashboard UI (open in browser to view traces, metrics, and logs)
- **Port 4317** — OTLP gRPC endpoint (default for the agent to export telemetry)

If you prefer, use the included helper script, which runs the same pinned dashboard image:

```powershell
./start_dashboard.ps1
```

> Check the container logs (`docker logs aspire-dashboard`) for the dashboard login token.

### Configure Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [Configure authentication in a JavaScript agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-authentication-for-javascript).
   1. Rename `env.TEMPLATE` to `.env`.
   1. Find the `connections` section and fill in the values:
      ```bash
      connections__serviceConnection__settings__clientId={{clientId}}
      connections__serviceConnection__settings__clientSecret={{clientSecret}}
      connections__serviceConnection__settings__tenantId={{tenantId}}
      ```
   1. Replace all **{{clientId}}** with the App Registration Id.
   1. Replace all **{{tenantId}}** with the Tenant Id where your application is registered.
   1. Set the **{{clientSecret}}** to the Secret that was created on the App Registration.

1. Set the OTLP endpoint in `.env`:

   ```bash
   OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
   ```

1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Running the Agent

1. In the agent's root directory, install dependencies:

   ```bash
   npm install
   ```

1. Start the Agent:

   ```bash
   npm start
   ```

## Accessing the Agent

### Using the Agent in Agents Playground

1. Install the Agents Playground if it is not already available:

   ```bash
   winget install agentsplayground
   ```

1. Start Agents Playground:

   ```bash
   agentsplayground
   ```

1. Interact with the agent through the browser.

### Optional: Using the Agent in WebChat

1. Go to your Azure Bot Service resource in the Azure Portal and select **Test in WebChat**

## OpenTelemetry Configuration

The `src/instrumentation.ts` file configures the OpenTelemetry Node SDK before the agent starts. It is loaded via the `--import` flag in the `start` script:

```json
"start": "node --env-file .env --import ./dist/instrumentation.js ./dist/agent.js"
```

The `src/agentTelemetry.ts` file defines the shared telemetry helpers (tracer, counters, histograms, and structured log emitters) used by the agent handlers.

By default, telemetry is exported to `http://localhost:4317` via OTLP gRPC. To change the endpoint, set the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable in your `.env` file.

### What is instrumented

| Signal | Sources |
|--------|---------|
| **Traces** | HTTP server instrumentation, shared sample spans (`agent.welcome_message`, `agent.message_handler`), and Agents SDK spans |
| **Metrics** | `agent.routes.executed.count`, `agent.message.processing.duration` |
| **Logs** | OTLP log records emitted by the sample for welcome and message handling |

## Viewing Telemetry

1. Open the Aspire Dashboard at `http://localhost:18888`.
1. Send a few messages to the agent.
1. In the dashboard, verify the shared telemetry contract:
   - **Traces** — `agent.welcome_message` and `agent.message_handler`
   - **Metrics** — `agent.routes.executed.count` and `agent.message.processing.duration`
   - **Logs** — welcome and message handling log records emitted by the sample

## Further reading

- [OpenTelemetry JS SDK](https://opentelemetry.io/docs/languages/js/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)

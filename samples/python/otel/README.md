# OpenTelemetry Agent

This is a sample of a simple Agent that is hosted on a Python web service. The sample demonstrates how to configure [OpenTelemetry](https://opentelemetry.io/) (OTel) for distributed tracing, metrics, and logging in a Microsoft 365 Agents SDK application.

The sample exports telemetry via OTLP (gRPC) to a configurable endpoint and automatically instruments the `aiohttp` server, `aiohttp` client, and `requests` libraries.

## Prerequisites

- [Python](https://www.python.org/) version 3.9 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)
- [Docker](https://www.docker.com/) (to run the Aspire Dashboard for local telemetry visualization)

## Local Setup

### Start the Telemetry Dashboard

This sample includes a PowerShell script to launch the [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview) as an OTLP collector and visualization UI.

```powershell
./start_dashboard.ps1
```

This runs the Aspire Dashboard container with:
- **Port 18888** — Dashboard UI (open in browser to view traces, metrics, and logs)
- **Port 4317** — OTLP gRPC endpoint (default for the agent to export telemetry)

### Configure Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
      2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
      3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.

1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Take note of the url shown after `Connect via browser:`

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Running the Agent

1. Open this folder from your IDE or Terminal of preference
1. (Optional but recommended) Set up virtual environment and activate it.
1. Install dependencies

```sh
pip install -r requirements.txt
```

### Run in localhost, anonymous mode

1. Start the application

```sh
python -m src.main
```

At this point you should see the message

```text
======== Running on http://localhost:3978 ========
```

The agent is ready to accept messages.

## Accessing the Agent

### Using the Agent in WebChat

1. Go to your Azure Bot Service resource in the Azure Portal and select **Test in WebChat**

## OpenTelemetry Configuration

The `telemetry.py` files provides the `configure_otel_providers` function, which sets up tracing, metrics, and logging before the agent starts:

```python
from telemetry import configure_otel_providers

configure_otel_providers(service_name="quickstart_agent")
```

By default, telemetry is exported to `http://localhost:4317/` via OTLP gRPC. To change the endpoint, set the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable in your `.env` file:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-collector:4317/
```

### What is instrumented

| Signal | Description |
|--------|-------------|
| **Traces** | HTTP spans for every incoming request (via `opentelemetry-instrumentation-aiohttp-server`) and outgoing requests (via `opentelemetry-instrumentation-aiohttp-client` and `opentelemetry-instrumentation-requests`) |
| **Metrics** | Exported on a periodic interval using `PeriodicExportingMetricReader` |
| **Logs** | Python `logging` records are forwarded to the OTLP log exporter via `LoggingHandler` |

## Further reading

- [OpenTelemetry Python](https://opentelemetry-python.readthedocs.io/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)

For more information on standard logging configuration, see the logging section in the [Quickstart Agent sample README](../quickstart/README.md).

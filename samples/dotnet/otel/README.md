# OTelAgent Sample (OpenTelemetry + Microsoft 365 Agents SDK)

This is a sample of a simple Agent hosted on an ASP.NET Core web service. The sample demonstrates how to configure [OpenTelemetry](https://opentelemetry.io/) (OTel) for distributed tracing, metrics, and logging in a Microsoft 365 Agents SDK application.

The sample exports telemetry via OTLP (gRPC) to a configurable endpoint and instruments ASP.NET Core, `HttpClient`, the .NET runtime, and the Agents SDK telemetry source, along with shared sample-level route telemetry for welcome and message handling.

The sample helps you:
- Understand the Microsoft 365 Agents SDK messaging loop.
- Learn how to integrate OpenTelemetry in an Agent (configuration, custom telemetry, enrichment).
- Export telemetry data to the Aspire Dashboard for local visualization and debugging.

## Prerequisites

- [.NET](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
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

1. Create an Azure Bot with one of these authentication types:
   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)
   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials)
   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)

   > **IMPORTANT:** If you want to run your agent locally via devtunnels, the only supported auth type is ClientSecrets and Certificates.

1. Update `appsettings.json` with your bot credentials:
   ```json
   "Connections": {
     "ServiceConnection": {
       "Settings": {
         "ClientId": "{{ClientId}}",
         "ClientSecret": "{{ClientSecret}}",
         "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}"
       }
     }
   }
   ```

1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Running the Agent

1. Start the Agent in Visual Studio or from the command line:

   ```bash
   dotnet run
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

The `AgentOtelExtension.cs` file provides the `ConfigureOtelProviders` extension method, which wires up all three OTel signals before the app starts:

```csharp
builder.ConfigureOtelProviders();
```

The `AgentTelemetry.cs` file defines the shared telemetry helpers (ActivitySource, counters, histograms) used by the agent handlers.

By default, telemetry is exported to `http://localhost:4317` via OTLP gRPC. To change the endpoint, set the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable or configure it in `appsettings.json`.

### What is instrumented

| Signal | Sources |
|--------|---------|
| **Traces** | ASP.NET Core requests, `HttpClient` outgoing calls, Agents SDK (`AgentsTelemetry.ActivitySource`), and shared sample spans (`agent.welcome_message`, `agent.message_handler`) |
| **Metrics** | ASP.NET Core, `HttpClient`, .NET runtime, Agents SDK meter, `agent.routes.executed.count`, `agent.message.processing.duration` |
| **Logs** | All `ILogger` log records forwarded to the OTLP log exporter, including shared app logs emitted from the sample handlers |

### Azure Monitor (Application Insights)

The sample includes a commented-out block for exporting to Azure Monitor. To enable it, add the `Azure.Monitor.OpenTelemetry.AspNetCore` NuGet package and uncomment the following in `AgentOtelExtension.cs`:

```csharp
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry()
       .UseAzureMonitor();
}
```

Then set `APPLICATIONINSIGHTS_CONNECTION_STRING` to your Application Insights connection string.

## Viewing Telemetry

1. Open the Aspire Dashboard at `http://localhost:18888`.
1. Send a few messages to the agent.
1. In the dashboard, verify the shared telemetry contract:
   - **Traces** — `agent.welcome_message` and `agent.message_handler`
   - **Metrics** — `agent.routes.executed.count` and `agent.message.processing.duration`
   - **Logs** — welcome and message handling log records emitted by the sample

## Further reading

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)

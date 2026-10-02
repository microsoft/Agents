# Slack / Copilot Studio Bridge (Microsoft 365 Agents SDK + OpenTelemetry)

This .NET 10 ASP.NET Core agent bridges Microsoft 365 Agents SDK channels to
Microsoft Copilot Studio. For Slack messages, it also uses Azure OpenAI to
convert Copilot Studio responses and attachments into validated Slack Block Kit
content. The project is based on the Agents SDK OpenTelemetry sample and
includes distributed tracing, metrics, and logging.

## Copilot Studio response handling

`OnMessageAsync` (non-Slack) and `OnSlackMessageAsync` share
`ProcessCopilotStudioResponseAsync`. `CopilotStudioTool` starts or resumes the
Copilot Studio conversation and returns the original response activities rather
than stripping them down to strings.

`CopilotStudioResponse.CollectAsync` aggregates final streaming messages and
ordinary messages in a `StringBuilder`, separating distinct messages with a blank
line. Intermediate streaming text is not appended because the final message
contains the complete answer. Attachments from message and typing activities are
collected, unchanged and in arrival order, into an `Attachment[]`. Informative
updates are kept separate from the answer.

- **Non-Slack:** Informative updates are forwarded while Copilot Studio works.
  Once collection completes, the buffered answer is queued through
  `StreamingResponse.QueueTextChunk`, attachments are added to the final response,
  and `EndStreamAsync` completes delivery.
- **Slack:** The collected `CopilotStudioResponse` is available in
  `turnState.Temp` under `McsSlackBridge.CopilotStudioResponseStateKey`
  (`copilotStudio.response`). The handler uses `GetClientAgent` with the existing
  Azure OpenAI-backed `IChatClient` and `GetConversationThread` to create or
  restore the formatter session. Slack task updates report Copilot Studio and
  formatting progress. The handler sends the aggregate text and serialized
  original attachments to the formatter model, validates the returned Block Kit
  content, and posts the final answer directly with
  `ISlackTurnContext.Client.CallAsync("chat.postMessage", ...)`. The destination
  channel, existing parent thread, and API token come only from the Slack context,
  never from model output. The original answer is not echoed through the Agents
  SDK streaming response.

### Slack formatting and session state

The formatter requests JSON with `text` (an accessibility/notification equivalent)
and `blocks`. Supported blocks are sections, headers, dividers, images, and context.
Instructions convert Markdown and inline Adaptive Card content to those blocks,
preserve file links, and represent open-URL actions as links. File downloads/uploads
and interactive submit/input actions are not implemented.

`SlackMessageContent` checks the content shape, supported block types, required
fields, HTTP(S) image URLs, and message/block size limits before posting. Invalid
or oversized output raises an error instead of being silently truncated or replaced
with an echo. Model and Slack API failures reach the existing logged turn-error
handler. Empty Copilot Studio responses are logged and are not sent to the formatter.

The Copilot Studio conversation ID is stored in conversation state as
`MCSConversationId`. After successful Slack delivery, the formatter session is
saved as serialized JSON in
`turnState.Conversation["conversation.threadInfo"]` and restored on the next
turn. The original response in temporary state remains turn-local. Conversation
state uses `MemoryStorage`, so neither session survives an application restart
or reliably spans multiple application instances.

Cancellation is passed through token exchange, Copilot Studio calls, response
collection, model formatting, final Slack posting, and non-Slack delivery.
Processing errors continue through the existing turn-error handler; non-Slack
stream completion failures are surfaced rather than reported as successful
delivery.

## Required configuration

The checked-in `appsettings.json` defines the base telemetry endpoint, Azure
OpenAI and Copilot Studio placeholders, outbound-host validation behavior,
token validation, message-processing defaults, and bot service connection.
Environment-specific values can be supplied with environment variables, user
secrets, or an environment-specific settings file.

| Configuration key | Purpose |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint used for traces, metrics, and logs |
| `TokenValidation:Audiences` | Accepted Azure Bot application/client IDs |
| `TokenValidation:TenantId` | Microsoft Entra tenant used to validate callers |
| `Connections:ServiceConnection:Settings:*` | Bot Framework service authentication |
| `ConnectionsMap` | Maps incoming service URLs or audiences to named connections |
| `AgentApplication:UserAuthorization:Handlers:mcs:*` | User token exchange used to call Copilot Studio |
| `AgentApplication:StartTypingTimer` | Enables or disables automatic typing updates |
| `AgentApplication:RemoveRecipientMention` | Controls removal of the bot mention from incoming text |
| `AgentApplication:NormalizeMentions` | Controls normalization of incoming mentions |
| `CopilotStudioAgent:DirectConnectUrl` | Copilot Studio agent runtime endpoint |
| `AIServices:AzureOpenAI:Endpoint` | Azure OpenAI resource endpoint |
| `AIServices:AzureOpenAI:DeploymentName` | Chat model deployment used for Slack formatting |
| `AIServices:AzureOpenAI:ApiKey` | Azure OpenAI credential; required at startup when the chat client is resolved |
| `OutboundHostValidator:*` | Controls outbound destination validation performed by the Agents SDK |

The base settings intentionally do not contain the Azure OpenAI API key. They
also do not define the required `mcs` user-authorization handler;
`appsettings.development.json` provides a development example. Supply that
handler in every active environment that calls Copilot Studio.

Do not commit real client secrets or Azure OpenAI API keys. This project has a
`UserSecretsId`, so local secrets can be set from the repository root:

```powershell
dotnet user-secrets set "Connections:ServiceConnection:Settings:ClientSecret" "<client-secret>"
dotnet user-secrets set "AIServices:AzureOpenAI:ApiKey" "<azure-openai-api-key>"
```

`appsettings.development.json` overrides the base configuration with
development-specific identifiers, connection mappings, authorization handlers,
and service endpoints. Replace them with values for your own tenant, bot,
Copilot Studio agent, and Azure OpenAI deployment.

The base configuration currently disables outbound-host validation. Review and
restrict `OutboundHostValidator` for production deployments.

The checked-in `appManifest/manifest.json` also retains names, descriptions,
URLs, and placeholders from the original OpenTelemetry sample. Replace that
metadata before packaging or distributing the Microsoft 365 app. Before zipping
the manifest for upload to Teams, replace every `${{AAD_APP_CLIENT_ID}}`
placeholder with the app (client) ID used for the Azure Bot Service OAuth
configuration.

## Tests

```powershell
dotnet test tests\Slack_MCS_Bridge.Tests\Slack_MCS_Bridge.Tests.csproj
```

The suite currently contains 45 tests and runs without live Slack, Copilot
Studio, or Azure OpenAI credentials.
Slack delivery tests use the real agent/session helpers and Slack API client with
a fake chat model and HTTP transport, including session reuse, attachment-only
responses, progress streaming, content limits, error propagation, and
context-controlled routing.

## OpenTelemetry

The application exports telemetry through OTLP and instruments ASP.NET Core,
`HttpClient`, the .NET runtime, and the Agents SDK telemetry source, along with
project-level route telemetry for welcome and message handling.

This instrumentation helps you:
- Understand the Microsoft 365 Agents SDK messaging loop.
- Learn how to integrate OpenTelemetry in an Agent (configuration, custom telemetry, enrichment).
- Export telemetry data to the Aspire Dashboard for local visualization and debugging.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)
- An Azure Bot registration with the required channel configuration
- A published Copilot Studio agent and a configured user-authentication connection
- An Azure OpenAI resource and chat model deployment
- [Docker](https://www.docker.com/) (optional, for the local Aspire telemetry dashboard)

## Local Setup

### Start the optional telemetry dashboard

Run the [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) locally with Docker:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:9.2
```

This exposes:
- **Port 18888** — Dashboard UI (open in browser to view traces, metrics, and logs)
- **Port 4317** — Host OTLP gRPC endpoint used by the checked-in base settings

If you prefer, use the included helper script, which runs the same pinned dashboard image:

```powershell
./start_dashboard.ps1
```

> Check the container logs (`docker logs aspire-dashboard`) for the dashboard login token.

### Provision Azure Bot Service and identity

1. Create an Azure Bot with one of these authentication types:
   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)
   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials)
   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)

   For guided provisioning, install the
   [`agents-sdk-common`](https://github.com/microsoft/Agents/tree/main/agent-plugins)
   plugin and use its `agents-sdk-provision` skill. The skill covers Azure Bot
   resource creation, identity credentials, OAuth configuration, and the
   corresponding Azure CLI workflow. Review generated commands and scopes
   before running them in your subscription.

   The current project configuration uses single-tenant client-secret
   authentication. Other authentication types require corresponding connection
   configuration changes.

1. Configure the bot connection, user-authorization handler, Copilot Studio
   runtime URL, and Azure OpenAI settings described in
   [Required configuration](#required-configuration). Keep credentials in user
   secrets or environment variables.

1. Replace the non-secret placeholders in `TokenValidation`,
   `Connections:ServiceConnection`, `AIServices:AzureOpenAI`, and
   `CopilotStudioAgent`. Configure `ConnectionsMap` and the `mcs`
   user-authorization handler for the target environment.

### Configure the application endpoint

1. Create and host a [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)
   with anonymous access. Port 3978 matches the checked-in
   `Slack_MCS_Bridge` launch profile:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Configure Slack support in ABS

Follow Microsoft's
[Connect a bot to Slack](https://learn.microsoft.com/azure/bot-service/bot-service-channel-connect-slack)
guide for the current Azure Bot Service redirect, event, action, and regional
endpoint values.

1. Create a Slack app in the target workspace. See Slack's
   [app settings quickstart](https://docs.slack.dev/app-management/quickstart-app-settings/).
1. Configure **OAuth & Permissions** and request only the
   [bot scopes](https://docs.slack.dev/reference/scopes/) needed by the events
   and features you enable.
1. Enable the [Events API](https://docs.slack.dev/apis/events-api/) and subscribe
   to the bot events required by your conversation types. Use the request URL
   supplied by the Microsoft guide rather than the local `/api/messages`
   endpoint.
1. Record the Slack Client ID, Client Secret, and Signing Secret in a secure
   location. Do not commit Slack credentials or tokens.
1. In the Azure Bot resource, open **Channels**, select **Slack**, enter the
   Slack app credentials, apply the configuration, and complete the workspace
   installation.
1. Add the app to the required Slack conversations and verify direct messages,
   channel messages, and thread replies used by your deployment.

This bridge formats final Slack responses with
[Block Kit](https://docs.slack.dev/block-kit/) and posts them with
`chat.postMessage`. Scope and event choices must therefore allow the app to
receive the intended messages and post to the intended conversations.

### Running the Agent

1. Start the Agent in Visual Studio or from the command line:

   ```powershell
   dotnet run --launch-profile Slack_MCS_Bridge
   ```

   The profile sets `ASPNETCORE_ENVIRONMENT` to `Development` and listens on
   `http://localhost:3978`. Visual Studio also opens the application in a
   browser when this profile starts.

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

The base settings export telemetry to `http://localhost:4317` via OTLP gRPC. To
change the endpoint, set the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable
or configure it in the active settings file.

`EnableOtlpExporter` is present in the settings files but is not read by
`ConfigureOtelProviders`; the OTLP exporters are always registered by the
current implementation.

> **Sensitive-data warning:** The current sample configuration records POST
> request bodies, outgoing HTTP request and response bodies, HTTP headers, and
> model content in telemetry. Do not enable this behavior in production without
> redaction and an appropriate data-handling review.

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

## JWT token validation

`AddAgentAspNetAuthentication` registers JWT bearer validation whenever the
application starts. At least one GUID audience is required. Replace
`{{ClientId}}` and `{{TenantId}}` in `appsettings.json` with values from the
Azure Bot registration:

```json
"TokenValidation": {
  "Audiences": [
    "{{ClientId}}"
  ],
  "TenantId": "{{TenantId}}"
}
```

The `Enabled` value currently present in `appsettings.development.json` is not
read by this project's `TokenValidationOptions`; it does not disable the
registered JWT bearer handler.

## Further reading

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)

# Named Pipe Agent Sample (.NET)

This sample demonstrates a **pipe-only** agent — it accepts activities exclusively over named pipes via the `Microsoft.Agents.Hosting.DirectLine.NamedPipes` library. It is the canonical shape used when deploying behind the **DirectLine App Service extension** (a.k.a. DirectLineFlex), where the sidecar relays traffic to the agent over a named pipe instead of HTTP.

The agent echoes the text of each request back to the caller, prefixed with a per-conversation counter.

> **Note:** The named-pipe transport currently is only designed to work with DirectLine App Service extension (DirectLineFlex). The DirectLine App Service extension (DirectLineFlex) that pairs with this agent over the pipe is only available on **Windows** App Service, so this sample is intended for Windows deployment targets.

## What's different from the QuickStart sample

Compared to [quickstart](../quickstart/README.md), this sample:

- **Adds** the `Microsoft.Agents.Hosting.DirectLine.NamedPipes` package and calls `builder.AddAgentNamedPipeTransport();`.
- **Omits** HTTP endpoint mapping (`MapAgentRootEndpoint`, `MapAgentApplicationEndpoints`).
- **Omits** ASP.NET authentication wiring (`AddAgentAspNetAuthentication`, `UseAuthentication`, `UseAuthorization`).

`AddAgentNamedPipeTransport()` adds:

- A hosted service that listens on the named-pipe server pair (`bfv4.pipes.incoming` / `bfv4.pipes.outgoing`).
- A delegating handler that routes outbound HTTP calls to `urn:botframework:namedpipe:*` back through the pipe.

> Because no HTTP endpoint is mapped, this sample is not directly reachable from the Bot Framework Emulator or Agents Playground. If you need an HTTP endpoint as well (for health checks or local Emulator testing), start from [quickstart](../quickstart/README.md) and add `builder.AddAgentNamedPipeTransport()` to that project.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Windows** deployment target — the DirectLine App Service extension (DirectLineFlex) that connects to this agent is only available on Windows App Service.

## Running locally

```bash
dotnet run
```

When the process starts, the agent:

- **Does not** expose `/api/messages` (no HTTP handlers are mapped).
- **Does not** require external authentication configuration.
- Creates the named-pipe server pair (`bfv4.pipes.incoming` / `bfv4.pipes.outgoing`) and waits for a client to connect.

To exercise the agent locally over the named pipe, you need a process on the same machine that connects as the named-pipe client (the role normally played by the DirectLine App Service extension sidecar). For a no-pipe local interactive loop, use the [quickstart](../quickstart/README.md) sample instead.

### Custom pipe name

The DirectLine App Service extension uses the pipe name `{WEBSITE_SITE_NAME}.directline`. To use a different pipe name, pass it to the extension:

```csharp
builder.AddAgentNamedPipeTransport("my-custom-pipe");
```

## Architecture

```
┌──────────────────────┐              ┌─────────────────────┐
│  DirectLineFlex      │──named pipe──│  NamedPipeAgent     │
│  Sidecar (client)    │              │  (this sample)      │
└──────────────────────┘              └─────────────────────┘
     Handles:                              Handles:
     - External auth (JWT)                 - Activity processing
     - TLS termination                     - Echo responses
     - WebSocket ↔ Pipe relay              - State management
```

The pipe is a trusted channel — the sidecar handles external authentication, so no JWT token validation is performed on the agent side.

## Deployment to Azure App Service

When deployed to Azure App Service with the DirectLine App Service extension enabled:

1. The App Service sidecar connects to your agent over the named pipe pair.
2. External traffic, authentication, and TLS are handled by the sidecar.
3. The pipe connection is treated as trusted; no JWT validation is performed on the pipe.

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [`Microsoft.Agents.Hosting.DirectLine.NamedPipes` package](https://www.nuget.org/packages/Microsoft.Agents.Hosting.DirectLine.NamedPipes)
- [Configure .NET bot for extension](https://learn.microsoft.com/en-us/azure/bot-service/bot-service-channel-directline-extension-net-bot)
- [QuickStart sample](../quickstart/README.md) — HTTP-based base sample
- [Node.js Named Pipe Agent sample](../../nodejs/named-pipe-agent/README.md) — equivalent sample in JavaScript

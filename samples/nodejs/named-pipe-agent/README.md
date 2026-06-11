# Named Pipe Agent Sample (Node.js)

This sample demonstrates a **pipe-only** agent — it accepts activities exclusively over named pipes via the `@microsoft/agents-hosting-directline-namedpipes` package. It is the canonical shape used when deploying behind the **DirectLine App Service extension** (a.k.a. DirectLineFlex), where the sidecar relays traffic to the agent over a named pipe instead of HTTP.

The agent echoes the text of each request back to the caller, prefixed with a per-conversation counter.

> **Note:** The named-pipe transport currently is only designed to work with DirectLine App Service extension (DirectLineFlex). The DirectLine App Service extension (DirectLineFlex) that pairs with this agent over the pipe is only available on **Windows** App Service, so this sample is intended for Windows deployment targets.

## What's different from the QuickStart sample

Compared to [quickstart](../quickstart/README.md), this sample:

- **Uses** `createLocalAdapter()` instead of `CloudAdapter` — no credentials needed.
- **Uses** `startNamedPipeServer()` instead of `startServer()` — no HTTP endpoint.
- **Omits** Express, `.env` auth config, and JWT middleware entirely.

## Prerequisites

- [Node.js](https://nodejs.org) version 20 or higher

    ```bash
    node --version
    ```

- **Windows** — named pipes are a Windows-only transport.

## Running locally

1. Open this folder from your IDE or terminal of preference.
1. Install dependencies and build:

    ```sh
    npm install
    npm run build
    ```

1. Copy the environment template and start the agent:

    ```powershell
    Copy-Item env.TEMPLATE .env   # edit if needed
    npm start
    ```

When the process starts, the agent:

- **Does not** expose an HTTP endpoint (no Express, no `/api/messages`).
- **Does not** require Azure/Entra authentication configuration.
- Creates the named-pipe server pair (`bfv4.pipes.incoming` / `bfv4.pipes.outgoing`) and waits for a client to connect.

To exercise the agent locally over the named pipe, you need a process on the same machine that connects as the named-pipe client (the role normally played by the DirectLine App Service extension sidecar). For a no-pipe local interactive loop, use the [quickstart](../quickstart/README.md) sample instead.

### Custom pipe name

The DirectLine App Service extension uses the pipe name `{WEBSITE_SITE_NAME}.directline`. Set the pipe name via the `PIPE_NAME` environment variable (defaults to `bfv4.pipes`):

```powershell
$env:PIPE_NAME='{WEBSITE_SITE_NAME}.directline'; npm start
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

The pipe is a trusted channel — the sidecar handles external authentication, so no JWT token validation is needed on the agent side.

## Key APIs used

| API | Purpose |
|-----|---------|
| `createLocalAdapter()` | Creates a `CloudAdapter` configured for pipe-only use (no credentials) |
| `startNamedPipeServer(adapter, logic, options)` | Starts the pipe server with auto-reconnect |
| `service.ready` | Promise that resolves when the first connection is established |
| `service.stop()` | Graceful shutdown |

## Deployment to Azure App Service

When deployed to Azure App Service with the DirectLine App Service extension enabled:

1. The App Service sidecar connects to your agent over the named pipe pair.
2. External traffic, authentication, and TLS are handled by the sidecar.
3. The pipe connection is treated as trusted; no JWT validation is performed on the pipe.
4. Set the pipe name via the `PIPE_NAME` env var to match the platform expectation.

## Further reading

- [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
- [`@microsoft/agents-hosting-directline-namedpipes` package](https://www.npmjs.com/package/@microsoft/agents-hosting-directline-namedpipes)
- [Configure Node.js bot for extension](https://learn.microsoft.com/en-us/azure/bot-service/bot-service-channel-directline-extension-node-bot)
- [QuickStart sample](../quickstart/README.md) — HTTP-based base sample
- [.NET Named Pipe Agent sample](../../dotnet/named-pipe-agent/README.md) — equivalent sample in .NET

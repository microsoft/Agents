---
name: agents-sdk-debugging
description: Use when attempting to resolve problems with an agent built using Microsoft Agents SDK @microsoft/agents-hosting and related packages.
---

# Debugging Agents Built with Microsoft Agents SDK

## Overview

Most agent failures fall into one of three categories: the code doesn't build or start, the configuration is wrong, or the agent isn't reachable. Work through this checklist in order — each step confirms a prerequisite for the next.

## Checklist

You MUST create a task for each of these items and complete them in order:

1. Make sure the code builds successfully.
2. Make sure the application starts and runs without crashing.
3. Make sure the application opens a port and listens for incoming requests.
4. Validate the `.env` configuration.
5. Validate the bot's credentials against Azure AD.
6. Use the Agents Playground to test the agent end-to-end locally.

---

### 1. Build the code

```bash
npm run build
```

Expected: exits with code 0, no errors. Fix any TypeScript or import errors before continuing.

---

### 2. Start the application

Run with debug logging enabled to get detailed output from the SDK internals:

```bash
DEBUG=agents:* npm start
```

Or for anonymous local dev:

```bash
DEBUG=agents:* node ./dist/index.js
```

The `DEBUG=agents:*` flag enables verbose logging across all SDK namespaces. Scope down to reduce noise:

```bash
DEBUG=agents:* npm start                    # everything
DEBUG=agents:authorization:* npm start      # all auth (most useful starting point)
DEBUG=agents:msal npm start                 # token acquisition only
```


#### Auth & connections

| Namespace | What it logs |
|---|---|
| `agents:authorization:connections` | Auth connections loaded at startup (clientId, tenantId, authType); which connection is selected per request |
| `agents:authorization:manager` | Auth handlers configured at startup (type, scopes); which handler is invoked per request |
| `agents:authorization:azurebot` | Azure Bot sign-in flow detail (token exchange, magic code, SSO) |
| `agents:authorization:agentic` | Agentic auth flow detail (token acquisition, OBO) |
| `agents:authorization` | High-level authorization middleware decisions |
| `agents:msal` | MSAL token acquisition (token requests, cache hits, OBO) |
| `agents:jwt-middleware` | Incoming JWT validation |
| `agents:authConfiguration` | Auth configuration loading |

#### Adapter & request handling

| Namespace | What it logs |
|---|---|
| `agents:cloud-adapter` | Incoming request processing, activity dispatch |
| `agents:base-adapter` | Base adapter lifecycle |
| `agents:connector-client` | Outbound calls to the Bot Connector service |
| `agents:user-token-client` | User token client requests |

#### Application & state

| Namespace | What it logs |
|---|---|
| `agents:app` | AgentApplication routing and lifecycle |
| `agents:activity-handler` | ActivityHandler event dispatch |
| `agents:state` | State read/write operations |
| `agents:turnState` | Turn state access |
| `agents:memory-storage` | MemoryStorage read/write |
| `agents:middleware` | Middleware pipeline execution |

#### Streaming, attachments & transcripts

| Namespace | What it logs |
|---|---|
| `agents:streamingResponse` | Streaming response lifecycle |
| `agents:attachmentDownloader` | Attachment download requests |
| `agents:M365AttachmentDownloader` | M365-specific attachment downloads |
| `agents:file-transcript-logger` | File transcript write operations |
| `agents:rest-client` | REST client calls (transcript middleware) |

#### Agent-to-agent

| Namespace | What it logs |
|---|---|
| `agents:agent-client` | Outbound agent client calls and response handling |

Watch for crash output. Common startup errors:

- **`Cannot find module`** — missing `npm install`, or `dist/` not built yet
- **`ERR_MODULE_NOT_FOUND`** — check `"type": "module"` in `package.json` and that imports use `.js` extensions
- **Port already in use** — another process is on port 3978; kill it or set `PORT` in `.env`

If the agent starts cleanly, you should see output like:

```
Server listening on port 3978
```

---

### 3. Confirm the agent is reachable

```bash
curl -s -o /dev/null -w "%{http_code}" \
  -X POST http://localhost:3978/api/messages \
  -H "Content-Type: application/json" \
  -d '{}'
```

| Response | Meaning |
|---|---|
| `401` | Agent is running, auth is active — this is correct for a configured agent |
| `200` | Agent is running with auth disabled (blank `clientId`) — correct for anonymous local dev |
| `000` or connection refused | Agent is not running, wrong port, or crashed on startup |

---

### 4. Validate `.env` configuration

Configuration mistakes are the most common source of failures. Check each area below.

#### 4a. Confirm the file is being loaded

The SDK requires `node --env-file .env` (Node 20+) or a manual `dotenv` call. If you're using `npm start`, check that `package.json` uses `--env-file`:

```json
"start": "node --env-file .env ./dist/index.js"
```

Without `--env-file`, environment variables silently don't load and the agent starts with no auth config.

#### 4b. Check for the correct env var format

The SDK uses the **modern `connections__` format**. Using the legacy flat format (`clientId=`, `clientSecret=`, `tenantId=`) still works but is only for backwards compatibility. Mixing the two formats causes silent misconfiguration.

**Modern format (use this):**
```
connections__serviceConnection__settings__clientId=<your-app-id>
connections__serviceConnection__settings__clientSecret=<your-secret>
connections__serviceConnection__settings__tenantId=<your-tenant-id>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

**Legacy format (avoid for new agents):**
```
clientId=<your-app-id>
clientSecret=<your-secret>
tenantId=<your-tenant-id>
```

#### 4c. Check double-underscore separators

A single underscore (`_`) is not the same as a double underscore (`__`). The SDK uses `__` to separate path segments. A typo like `connections_serviceConnection_settings_clientId` will be silently ignored.

#### 4d. Check `connectionsMap` entries

If you have multiple connections, each must have a `connectionsMap` entry. The first entry whose `serviceUrl` pattern matches the incoming request wins. Always include a `serviceUrl=*` fallback as the last entry.

With a single connection, `connectionsMap` can be omitted — the SDK defaults to `serviceUrl=*`.

#### 4e. Check OAuth handler variables

If your agent uses user sign-in (`authorization: { graph: { ... } }`), the OAuth connection name must be set:

```
graph_connectionName=GraphOAuthConnection
```

The prefix (`graph`) must match the key used in the `authorization` config in your code. A mismatch causes the sign-in flow to fail silently or with a cryptic error.

---

### 5. Validate bot credentials against Azure AD

Once the `.env` looks correct, confirm the credentials actually work by requesting a token:

```bash
curl -s -X POST \
  "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" \
  -d "grant_type=client_credentials\
&client_id=$clientId\
&client_secret=$clientSecret\
&scope=https://api.botframework.com/.default" \
  | jq '{token_type, expires_in, error, error_description}'
```

A successful response includes `access_token`. Common errors:

| Error code | Cause |
|---|---|
| `AADSTS700016` | `clientId` not found in tenant — wrong ID or wrong tenant |
| `AADSTS7000215` | Invalid `clientSecret` — expired or incorrect |
| `AADSTS90002` | `tenantId` not found |

---

### 6. Test with Agents Playground

The Agents Playground acts as a mock connector and channel client. It lets you test the full message flow locally without deploying to Azure or configuring a real Bot resource.

**Install:**
```bash
npm install -g agentsplayground
```

**Run against an anonymous agent (no `.env` needed):**
```bash
npm test
```

This assumes your `package.json` has:
```json
"start:anon": "node ./dist/index.js",
"test-tool": "agentsplayground -c emulator",
"test": "npm-run-all -p -r start:anon test-tool"
```

**Run against an authenticated agent:**
```bash
agentsplayground -c msteams \
  --client-id <your-app-id> \
  --client-secret <your-secret> \
  --tenant-id <your-tenant-id>
```

**Channel options** (`-c`): `msteams`, `webchat`, `directline`, `emulator`, `agents`

If the playground connects but messages don't get responses, the agent is running but a message handler may be missing or the route isn't matching. Add a fallback handler to confirm:

```typescript
this.onActivity('message', async (ctx: TurnContext) => {
  await ctx.sendActivity(`Echo: ${ctx.activity.text}`)
}, [], RouteRank.Last)
```

---

### Validate an OAuth connection name

OAuth connection names can only be tested end-to-end through a real sign-in flow:

**Azure Portal → Your Bot Resource → Settings → OAuth Connection Settings → [your connection] → Test Connection**

This confirms the connection name matches, the OAuth app has the right scopes, and the redirect URI (`https://token.botframework.com/.auth/web/redirect`) is registered on the app registration.

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

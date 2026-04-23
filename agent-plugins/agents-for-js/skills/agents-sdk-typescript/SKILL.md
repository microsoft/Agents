---
name: agents-sdk-typescript
description: Use when any code imports @microsoft/agents-hosting, @microsoft/agents-hosting-express, or related Agents SDK packages, or when building a new agent with the Microsoft 365 Agents SDK for TypeScript
---

## Overview

The Microsoft 365 Agents SDK builds multichannel agents for Teams, Copilot Studio, and web chat.

| Package | Purpose |
|---|---|
| `@microsoft/agents-hosting` | Core: AgentApplication, CloudAdapter, TurnContext, TurnState, storage |
| `@microsoft/agents-hosting-express` | `startServer()` convenience wrapper |
| `@microsoft/agents-hosting-storage-blob` | Azure Blob Storage backend |
| `@microsoft/agents-hosting-storage-cosmos` | CosmosDB backend |
| `@microsoft/agents-hosting-dialogs` | Dialog system |

Requires Node 18+. Use `node --env-file .env` (Node 20+) to load environment variables.

## Azure Resources Required

**Microsoft Entra App Registration**
- `clientId` — Application (client) ID
- `clientSecret` — Certificates & secrets
- `tenantId` — Directory (tenant) ID

**Azure Bot Resource**
- Messaging endpoint: `https://<your-host>/api/messages`
- Microsoft App ID must match `clientId`

Local dev: Use Bot Framework Emulator. No Azure Bot needed until deployment. Leave `clientId` blank to skip auth validation.

## Environment Variables

### Modern format (recommended)

Uses `connections__` and `connectionsMap__` prefixes. Double underscores (`__`) separate path segments; `.settings.` is stripped automatically.

**Single connection (most common):**
```
connections__serviceConnection__settings__clientId=<your-app-id>
connections__serviceConnection__settings__clientSecret=<your-secret>
connections__serviceConnection__settings__tenantId=<your-tenant-id>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

**How `connectionsMap` works:**
Each entry maps a `serviceUrl` pattern to a named connection. The first matching entry wins.
- `serviceUrl=*` — matches any service URL (use as the default/fallback)
- `serviceUrl` is treated as a regex for all other values

`connectionsMap` can be omitted when there is only one connection — the SDK defaults it to `serviceUrl=*`.

**Multiple connections** (e.g. different identities for different channels):
```
connections__mainConn__settings__clientId=<app-id-1>
connections__mainConn__settings__clientSecret=<secret-1>
connections__mainConn__settings__tenantId=<tenant-id>

connections__teamsConn__settings__clientId=<app-id-2>
connections__teamsConn__settings__clientSecret=<secret-2>
connections__teamsConn__settings__tenantId=<tenant-id>

connectionsMap__0__connection=teamsConn
connectionsMap__0__serviceUrl=https://smba.trafficmanager.net/.*
connectionsMap__1__connection=mainConn
connectionsMap__1__serviceUrl=*
```

Optional `audience` field on a map entry restricts matching to activities whose JWT `aud` claim equals that value:
```
connectionsMap__0__connection=teamsConn
connectionsMap__0__serviceUrl=*
connectionsMap__0__audience=<teams-app-id>
```

**Available connection settings fields:**
`clientId`, `clientSecret`, `tenantId`, `authority`, `certPemFile`, `certKeyFile`, `sendX5C`, `connectionName`, `scope`

### Legacy format — backwards compatibility only

> **Never use the legacy format for new agents.** It exists solely for backwards compatibility with older BotFramework-based bots. Always use the modern `connections__` format above.

```
clientId=<your-app-id>
clientSecret=<your-secret>
tenantId=<your-tenant-id>
```

Both `startServer()` and `loadAuthConfigFromEnv()` auto-detect the format. Leave `clientId` blank locally to skip auth.

### OAuth authorization handler variables

These control the user sign-in flow configured via `authorization: { [id]: { ... } }`. The `id` is the key used in the authorization options (e.g. `graph`).

**AzureBot handler** (default — user OAuth flow via Azure Bot Service):
```
graph_connectionName=GraphOAuthConnection   # required — OAuth connection name in Azure Bot resource
graph_connectionTitle=Sign in with Microsoft
graph_connectionText=Please sign in to continue
graph_maxAttempts=3                          # max magic code attempts (default: 2)
graph_enableSso=false                        # disable SSO (default: true)

# OBO (on-behalf-of) — auto-exchange on routes using exchangeToken()
graph_obo_connection=OBOConnection
graph_obo_scopes=https://graph.microsoft.com/.default,Mail.Read

# Custom error messages
graph_messages_invalidCode=That code was invalid, please try again.
graph_messages_invalidCodeFormat=Please enter the 6-digit code from the sign-in card.
graph_messages_maxAttemptsExceeded=Too many failed attempts. Please try again later.
```

**Agentic handler** (agent-to-agent, no user prompt):
```
myHandler_type=agentic
myHandler_scopes=https://graph.microsoft.com/.default   # comma-separated, required
myHandler_altBlueprintConnectionName=altConn             # optional
```

## Quick Start

```typescript
import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, MemoryStorage, TurnContext, TurnState } from '@microsoft/agents-hosting'

class MyAgent extends AgentApplication<TurnState> {
  constructor() {
    super({ storage: new MemoryStorage() })

    this.onConversationUpdate('membersAdded', async (ctx: TurnContext) => {
      await ctx.sendActivity('Hello! Send me a message.')
    })

    this.onActivity('message', async (ctx: TurnContext, state: TurnState) => {
      let counter: number = state.getValue('conversation.counter') || 0
      await ctx.sendActivity(`[${counter++}] You said: ${ctx.activity.text}`)
      state.setValue('conversation.counter', counter)
    })
  }
}

startServer(new MyAgent())
```

Run: `node --env-file .env dist/index.js`

## Server Setup

### Option A: startServer() (preferred)

`startServer(agent)` creates an Express app with:
- `express.json()` + `authorizeJWT()` middleware
- `POST /api/messages` route
- Listens on `PORT` env var (default 3978)
- **Returns the Express instance** — add extra routes to the return value

```typescript
import { startServer } from '@microsoft/agents-hosting-express'

const server = startServer(agent)
server.get('/health', (_req, res) => res.json({ ok: true }))
```

### Option B: Manual Express

```typescript
import express, { Response } from 'express'
import { Request, CloudAdapter, authorizeJWT, loadAuthConfigFromEnv } from '@microsoft/agents-hosting'

const authConfig = loadAuthConfigFromEnv()
const adapter = new CloudAdapter(authConfig)
const app = express()

app.use(express.json())
app.use(authorizeJWT(authConfig))

app.post('/api/messages', async (req: Request, res: Response) => {
  await adapter.process(req, res, async (context) => await agent.run(context))
})

app.listen(process.env.PORT || 3978)
```

### Proactive Messaging

Save a reference during a turn, then use `adapter.continueConversation` from any route. `req.user` comes from `authorizeJWT` middleware.

```typescript
import { ConversationReference } from '@microsoft/agents-activity'

// During a turn — save the reference
const ref = ctx.activity.getConversationReference()
conversationReferences[ref.conversation.id] = ref

// In a proactive route
app.get('/api/notify', async (req, res) => {
  for (const ref of Object.values(conversationReferences)) {
    await adapter.continueConversation(req.user!, ref, async (ctx) => {
      await ctx.sendActivity('Proactive message')
    })
  }
  res.json({ ok: true })
})
```

## Validating Your Configuration

### 1. Validate bot credentials (clientId / clientSecret / tenantId)

This tests that your Entra app registration credentials are correct and can authenticate with the Bot Framework. A successful response includes `access_token`; an error response includes `error` and `error_description`.

```bash
curl -s -X POST \
  "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" \
  -d "grant_type=client_credentials\
&client_id=$clientId\
&client_secret=$clientSecret\
&scope=https://api.botframework.com/.default" \
  | jq '{token_type, expires_in, error, error_description}'
```

Common errors:
- `AADSTS700016` — `clientId` not found in tenant (wrong ID or wrong tenant)
- `AADSTS7000215` — invalid `clientSecret` (expired or incorrect)
- `AADSTS90002` — `tenantId` not found

### 2. Validate the agent is running and reachable

```bash
curl -s -o /dev/null -w "%{http_code}" \
  -X POST http://localhost:3978/api/messages \
  -H "Content-Type: application/json" \
  -d '{}'
```

- `401` — agent is running; JWT auth rejected the empty request (expected — means auth is working)
- `000` or connection refused — agent is not running or wrong port
- `200` — agent is running with auth disabled (local dev with blank `clientId`)

### 3. Validate an OAuth connection name

OAuth connection names (used by `graph_connectionName`) can only be tested end-to-end through a real sign-in flow. Use the Azure portal:

**Azure Portal → Your Bot Resource → Settings → OAuth Connection Settings → [your connection] → Test Connection**

This confirms the connection name matches, the OAuth app has the right scopes, and the redirect URI is configured correctly.

## Local Testing with Agents Playground

The Agents Playground lets you test your agent locally without deploying to Azure or configuring a Bot resource. It acts as a mock connector service and channel client.

**Install:**
```bash
npm install -g agentsplayground
```

### Recommended package.json scripts

Include these scripts when creating a new agent's `package.json`. The `test` script starts the agent and playground together in parallel — no separate terminals needed:

```json
"scripts": {
  "prebuild": "npm ci",
  "build": "tsc --build",
  "prestart": "npm run build",
  "prestart:anon": "npm run build",
  "start:anon": "node ./dist/index.js",
  "start": "node --env-file .env ./dist/index.js",
  "test-tool": "agentsplayground -c emulator",
  "test": "npm-run-all -p -r start:anon test-tool"
}
```

- `npm start` — builds and runs with `.env` credentials
- `npm test` — builds, starts the agent without auth (`start:anon`), and launches the playground in parallel. Use this for quick local dev without needing an `.env` file.
- `-c emulator` — uses the emulator channel (no auth required). Change to `msteams`, `webchat`, etc. as needed.
- Requires `npm-run-all` as a dev dependency: `npm install -D npm-run-all`

**With authentication** (for testing OAuth/sign-in flows):
```bash
agentsplayground -c msteams \
  --client-id <your-app-id> \
  --client-secret <your-secret> \
  --tenant-id <your-tenant-id>
```

**Channel options** (`-c`): `msteams`, `webchat`, `directline`, `emulator`, `agents`

## AgentApplication Patterns

**Routing**
```typescript
this.onMessage('/cmd', handler)              // exact command
this.onActivity('message', handler)          // all messages
this.onConversationUpdate('membersAdded', handler)
this.onActivity('invoke', handler)
this.onActivity('message', fallback, [], RouteRank.Last) // fallback
```

**TurnState** — dot-notation keys auto-scoped to conversation/user/temp:
```typescript
const count = state.getValue('conversation.counter') || 0
state.setValue('conversation.counter', count + 1)
state.deleteValue('conversation.counter')
```

**Storage backends**

| Backend | Use case |
|---|---|
| `MemoryStorage` | Local dev only — not persistent |
| `BlobsStorage` | Azure Blob — production |
| `CosmosDbPartitionedStorage` | CosmosDB — production |

## Authorization (User Token Flow)

```typescript
class MyAgent extends AgentApplication<TurnState> {
  constructor() {
    super({
      storage: new MemoryStorage(),
      authorization: {
        graph: {
          name: 'GraphOAuthConnection', // OAuth connection name in Azure Bot resource
          title: 'Sign in with Microsoft',
          text: 'Please sign in to continue',
        }
      }
    })

    this.onSignInSuccess(async (ctx, state) => {
      const { token } = await this.authorization.getToken(ctx, 'graph')
      // use token to call external APIs
      await ctx.sendActivity('Signed in!')
    })

    this.onSignInFailure(async (ctx, state, authId, err) => {
      await ctx.sendActivity(`Sign-in failed: ${err}`)
    })

    // Protect a route — SDK sends the OAuth card automatically if not signed in
    this.onActivity('message', async (ctx, state) => {
      const { token } = await this.authorization.getToken(ctx, 'graph')
      // token is guaranteed here — route won't run until user is signed in
    }, ['graph'])

    // Sign out from all providers
    this.onMessage('/logout', async (ctx, state) => {
      await this.authorization.signOut(ctx, state)
      await ctx.sendActivity('Signed out.')
    })
  }
}
```

`name` can also be provided via environment variable `graph_connectionName` (where `graph` is the handler key) and omitted from code.

**OBO (on-behalf-of) — exchange user token for a downstream service token:**
```typescript
const { token } = await this.authorization.exchangeToken(ctx, 'graph', {
  scopes: ['https://graph.microsoft.com/.default']
})
// use token to call Graph or other downstream APIs
```

**Agentic auth** (agent-to-agent, no user prompt):
```typescript
authorization: { agentic: { type: 'agentic' } }
// env: agentic_type=agentic, agentic_scopes=https://graph.microsoft.com/.default
```

## Cards

Import `CardFactory` and `MessageFactory` from `@microsoft/agents-hosting`. Import `ActionTypes` from `@microsoft/agents-activity`.

**Adaptive Card** (from a JSON template):
```typescript
import AdaptiveCard from './resources/myCard.json'

const card = CardFactory.adaptiveCard(AdaptiveCard)
await ctx.sendActivity(MessageFactory.attachment(card))
```

**Hero Card:**
```typescript
const card = CardFactory.heroCard(
  'Card Title',
  CardFactory.images(['https://example.com/image.jpg']),
  CardFactory.actions([
    { type: ActionTypes.OpenUrl, title: 'Learn more', value: 'https://example.com' }
  ])
)
await ctx.sendActivity(MessageFactory.attachment(card))
```

**Thumbnail Card:**
```typescript
const card = CardFactory.thumbnailCard('Title', images, actions, {
  subtitle: 'Subtitle',
  text: 'Body text'
})
await ctx.sendActivity(MessageFactory.attachment(card))
```

Other factories: `CardFactory.animationCard`, `CardFactory.audioCard`, `CardFactory.videoCard`, `CardFactory.receiptCard`.

## Streaming

```typescript
this.onActivity('message', async (ctx: TurnContext) => {
  ctx.streamingResponse.setFeedbackLoop(true)
  ctx.streamingResponse.setGeneratedByAILabel(true)
  ctx.streamingResponse.queueInformativeUpdate('Working on it...')
  ctx.streamingResponse.queueTextChunk('Part 1 ')
  ctx.streamingResponse.queueTextChunk('Part 2')
  await ctx.streamingResponse.endStream() // required
})
```

## Common Mistakes

**1. Wrong field name on ConversationReference**

```typescript
// WRONG
const ref: ConversationReference = { bot: { id: appId } }

// CORRECT
const ref: ConversationReference = { agent: { id: appId } }
```

**2. JwtPayload.aud is string | string[]**

```typescript
// WRONG
const appId = payload.aud

// CORRECT
const appId = Array.isArray(payload.aud) ? payload.aud[0] : payload.aud
```

**3. Adapter callbacks swallow exceptions**

```typescript
// WRONG — error is lost
await adapter.continueConversation(identity, ref, async (ctx) => {
  throw new Error('something failed') // swallowed
})

// CORRECT — capture and rethrow
let capturedError: Error | undefined
await adapter.continueConversation(identity, ref, async (ctx) => {
  try {
    await doWork(ctx)
  } catch (err) {
    capturedError = err as Error
  }
})
if (capturedError) throw capturedError
```

**4. startServer() returns Express — add routes to the return value**

```typescript
// WRONG — routes never registered
const app = express()
startServer(agent)
app.get('/health', handler)

// CORRECT
const server = startServer(agent)
server.get('/health', handler)
```

### Wrong method names — `activity()` and `message()` don't exist

`AgentApplication` uses `on`-prefixed method names. Common wrong guesses:

```typescript
// ❌ wrong — these methods don't exist
app.activity('message', handler)
app.message(handler)

// ✅ correct
app.onActivity('message', handler)
app.onMessage('/help', handler)
app.onConversationUpdate('membersAdded', handler)
```

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

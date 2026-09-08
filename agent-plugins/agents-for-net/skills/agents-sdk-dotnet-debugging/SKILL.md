---
name: agents-sdk-dotnet-debugging
description: >
  Use when troubleshooting an agent built with the Microsoft Agents SDK
  (Microsoft.Agents.Hosting.AspNetCore and related packages) in C# / .NET.
  Trigger on any of these symptoms: build or C# compile errors, crashes on
  startup, 401 or auth errors on incoming requests, the bot not responding to
  messages, appsettings.json configuration problems, Azure AD credential
  failures (AADSTS errors), port conflicts, or the agent not connecting in
  Teams or the Agents Playground. Use even if the user doesn't mention the SDK
  by name — trigger on symptoms like "my bot won't start", "getting 401s", or
  "bot isn't responding."
---

# Debugging Agents Built with Microsoft Agents SDK (.NET)

## Overview

Most agent failures fall into one of three categories: the code doesn't build or start, the configuration is wrong, or the agent isn't reachable. Work through this checklist in order — each step confirms a prerequisite for the next.

## Checklist

You MUST create a task for each of these items and complete them in order:

1. Make sure the code builds successfully.
2. Make sure the application starts and runs without crashing.
3. Make sure the application opens a port and listens for incoming requests.
4. Validate the `appsettings.json` configuration.
5. Validate the bot's credentials against Azure AD.
6. Use the Agents Playground to test the agent end-to-end locally.

---

### 1. Build the code

```bash
dotnet build
```

Expected: exits with code 0, no errors. Fix any C# compile errors before continuing.

Common build errors:

- **Missing package** — `dotnet add package Microsoft.Agents.Hosting.AspNetCore`
- **Namespace not found** — wrong `using` statement; see the `agents-sdk-dotnet` skill for correct namespaces
- **Target framework mismatch** — Agents SDK requires .NET 8+; check `<TargetFramework>` in `.csproj`

---

### 2. Start the application

Run with detailed logging:

```bash
dotnet run
```

Or with verbose logging:

```bash
dotnet run --verbosity detailed
```

To enable debug-level SDK logging, add to `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Agents": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Watch for crash output. Common startup errors:

- **`Unable to resolve service for type 'IStorage'`** — missing `builder.Services.AddSingleton<IStorage, MemoryStorage>()` in `Program.cs`
- **`AddAgentAspNetAuthentication` missing** — add `builder.Services.AddAgentAspNetAuthentication(builder.Configuration)`
- **Port already in use** — another process is on the port; check with `netstat -ano | findstr :3978` (Windows) or `lsof -i :3978` (macOS/Linux)
- **`InvalidOperationException` on startup** — check `appsettings.json` structure, especially `Connections` and `ConnectionsMap`

If the agent starts cleanly, you should see output like:

```
Now listening on: http://localhost:3978
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
| `200` | Agent is running with auth disabled (`TokenValidation:Enabled = false`) — correct for local dev |
| `000` or connection refused | Agent is not running, wrong port, or crashed on startup |

---

### 4. Validate `appsettings.json` configuration

Configuration mistakes are the most common source of failures. Check each area below.

#### 4a. Confirm the file is being loaded

ASP.NET Core auto-loads `appsettings.json` and `appsettings.{Environment}.json`. Check that:
- The file exists in the project root
- It has `"Copy to Output Directory": "PreserveNewest"` in `.csproj` or is at the content root
- `ASPNETCORE_ENVIRONMENT` is set correctly (`Development` for local dev)

#### 4b. Check the Connections section structure

The SDK requires a specific JSON structure. Common mistakes:

```json
// WRONG — flat format (this is Node.js env var style, not appsettings)
{
  "ClientId": "...",
  "ClientSecret": "...",
  "TenantId": "..."
}

// CORRECT
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<appId>",
        "ClientSecret": "<secret>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  }
}
```

#### 4c. Check ConnectionsMap

If `ConnectionsMap` is present, it must be a JSON array with proper structure:

```json
// WRONG — object instead of array
"ConnectionsMap": {
  "ServiceUrl": "*",
  "Connection": "ServiceConnection"
}

// CORRECT — array
"ConnectionsMap": [
  {
    "ServiceUrl": "*",
    "Connection": "ServiceConnection"
  }
]
```

If omitted, the SDK defaults to mapping `*` to the first connection.

#### 4d. Check TokenValidation section

```json
{
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<your-app-id>"],
    "TenantId": "<your-tenant-id>"
  }
}
```

- `Audiences` must include your `ClientId`
- `TenantId` must match the tenant where the app registration lives
- Set `Enabled: false` for anonymous local development

#### 4e. Check OAuth handler configuration

If your agent uses user sign-in, verify the `AgentApplication:UserAuthorization` section:

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "graph",
      "AutoSignin": true,
      "Handlers": {
        "graph": {
          "Settings": {
            "AzureBotOAuthConnectionName": "GraphOAuthConnection",
            "Title": "Sign In",
            "Text": "Please sign in"
          }
        }
      }
    }
  }
}
```

The handler key (`graph`) must match the `autoSignInHandlers` used in route registration. A mismatch causes the sign-in flow to fail silently.

---

### 5. Validate bot credentials against Azure AD

Once `appsettings.json` looks correct, confirm the credentials actually work:

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
| `AADSTS700016` | `ClientId` not found in tenant — wrong ID or wrong tenant |
| `AADSTS7000215` | Invalid `ClientSecret` — expired or incorrect |
| `AADSTS90002` | `TenantId` not found |

---

### 6. Test with Agents Playground

The Agents Playground acts as a mock connector and channel client.

**Install:**
```bash
npm install -g agentsplayground
```

**Run against an anonymous agent:**

Start your agent:
```bash
dotnet run
```

In a separate terminal:
```bash
agentsplayground -c emulator
```

**Run against an authenticated agent:**
```bash
agentsplayground -c msteams \
  --client-id <your-app-id> \
  --client-secret <your-secret> \
  --tenant-id <your-tenant-id>
```

**Channel options** (`-c`): `msteams`, `webchat`, `directline`, `emulator`, `agents`

If the playground connects but messages don't get responses, the agent is running but a message handler may be missing. Add a fallback handler to confirm:

```csharp
OnActivity(ActivityTypes.Message, async (ctx, state, ct) =>
{
    await ctx.SendActivityAsync($"Echo: {ctx.Activity.Text}", cancellationToken: ct);
}, rank: RouteRank.Last);
```

---

### Common Runtime Errors

| Error | Cause | Fix |
|---|---|---|
| `Unable to resolve service for type 'IStorage'` | Missing storage registration | Add `builder.Services.AddSingleton<IStorage, MemoryStorage>()` |
| `MapAgentApplicationEndpoints` 404 | Using `MapAgentEndpoints` (compat) with `AgentApplication` | Switch to `MapAgentApplicationEndpoints` |
| `MapAgentEndpoints` 404 | Using `MapAgentApplicationEndpoints` with `ActivityHandler` | Switch to `MapAgentEndpoints` |
| `InvalidOperationException: No agent registered` | Missing `builder.AddAgent<T>()` | Add `builder.AddAgent<MyAgent>()` |
| 401 on every request | `TokenValidation:Enabled` is `true` with no/wrong credentials | Set to `false` for local dev, or fix credentials |
| `System.Text.Json.JsonException` on card deserialization | Wrong JSON structure in card content | Validate card JSON separately |
| OAuth sign-in card appears but token exchange fails | Wrong `AzureBotOAuthConnectionName` | Verify the connection name matches Azure Bot resource OAuth settings |
| Streaming chunks not appearing | Missing `EndStreamAsync` call | Always call `await ctx.StreamingResponse.EndStreamAsync(ct)` in a `finally` block |

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

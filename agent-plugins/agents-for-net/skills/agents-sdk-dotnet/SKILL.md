---
name: agents-sdk-dotnet
description: >
  Use when any code imports Microsoft.Agents.Hosting.AspNetCore,
  Microsoft.Agents.Builder, or related Agents SDK packages, or when the user is
  building, configuring, or asking questions about a Microsoft 365 Agents SDK
  agent in C# / .NET. Trigger on questions about appsettings.json,
  connection configuration, AgentApplication patterns, OAuth sign-in flows,
  storage backends, cards, streaming, or local testing with the Agents Playground
  — even if no code exists yet and the user is planning or asking how to get started.
---

## Overview

The Microsoft 365 Agents SDK builds multichannel agents for Teams, Copilot Studio, and web chat.

| Package | Purpose |
|---|---|
| `Microsoft.Agents.Hosting.AspNetCore` | ASP.NET Core hosting, auth, endpoint mapping |
| `Microsoft.Agents.Authentication.Msal` | MSAL-based auth (client credentials, OBO) |
| `Microsoft.Agents.Builder` | Core: AgentApplication, ITurnContext, ITurnState |
| `Microsoft.Agents.Core` | Models, activity types, channel accounts |
| `Microsoft.Agents.Storage` | IStorage, MemoryStorage |
| `Microsoft.Agents.Extensions.Teams` | Teams-specific models and extensions |
| `Microsoft.Agents.Builder.Dialogs` | Dialog system (waterfall, prompts) |
| `Microsoft.Agents.AI` | AI-specific features |

**Minimal project requires only:** `Microsoft.Agents.Hosting.AspNetCore` + `Microsoft.Agents.Authentication.Msal`. The others are transitive dependencies pulled in automatically.

**Always use the latest non-beta (stable) version.** Do not use `--prerelease` unless specifically needed.

Requires .NET 8+.

## Azure Resources Required

**Microsoft Entra App Registration**
- `ClientId` — Application (client) ID
- `ClientSecret` — Certificates & secrets
- `TenantId` — Directory (tenant) ID

**Azure Bot Resource**
- Messaging endpoint: `https://<your-host>/api/messages`
- Microsoft App ID must match `ClientId`

Local dev: Set `TokenValidation:Enabled` to `false`. No Azure Bot needed until deployment.

## Configuration (appsettings.json)

### Single connection (most common)

**appsettings.json** — no secret here (safe to commit):

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<appId>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    {
      "ServiceUrl": "*",
      "Connection": "ServiceConnection"
    }
  ],
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<appId>"],
    "TenantId": "<tenantId>"
  }
}
```

**appsettings.Development.json** — secret lives here (excluded via `.gitignore`):

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "ClientSecret": "<secret>"
      }
    }
  },
  "TokenValidation": {
    "Enabled": false
  }
}
```

### How ConnectionsMap works

Each entry maps a `ServiceUrl` pattern to a named connection. The first matching entry wins.
- `ServiceUrl: "*"` — matches any service URL (use as the default/fallback)
- Other values are treated as regex patterns

### Multiple connections (different identities per channel)

```json
{
  "Connections": {
    "MainConn": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<app-id-1>",
        "ClientSecret": "<secret-1>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    },
    "TeamsConn": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<app-id-2>",
        "ClientSecret": "<secret-2>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    {
      "ServiceUrl": "https://smba.trafficmanager.net/.*",
      "Connection": "TeamsConn"
    },
    {
      "ServiceUrl": "*",
      "Connection": "MainConn"
    }
  ]
}
```

### Auth type variants

**UserManagedIdentity** (no secret, Azure-hosted only):
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "UserManagedIdentity",
        "ClientId": "<msi-clientId>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  }
}
```

**FederatedCredentials** (no secret, App Registration + MSI):
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "FederatedCredentials",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<appId>",
        "FederatedClientId": "<msi-clientId>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  }
}
```

### Available connection settings fields

`AuthType`, `AuthorityEndpoint`, `ClientId`, `ClientSecret`, `TenantId`, `FederatedClientId`, `Scopes`, `CertificateSubject`, `CertificateThumbprint`

### Local development (disable token validation)

Put in `appsettings.Development.json` (not `appsettings.json`):

```json
{
  "TokenValidation": {
    "Enabled": false
  }
}
```

## Quick Start

**Prerequisite:** Copy [`AspNetExtensions.cs`](https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs) into your project. This provides `AddAgentAspNetAuthentication` for JWT token validation.

**Program.cs:**

```csharp
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.AddAgent<MyAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

app.Run();
```

**MyAgent.cs** — put the `AgentApplication` subclass in its own file (see [AgentApplication Patterns](#agentapplication-patterns) below for routing, state, and auth examples).

**Properties/launchSettings.json:**

```json
{
  "profiles": {
    "MyAgent": {
      "commandName": "Project",
      "launchBrowser": false,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "https://localhost:3979;http://localhost:3978"
    }
  }
}
```

Run: `dotnet run`

**.gitignore** — ensure `appsettings.Development.json` is excluded (it contains secrets):

```
appsettings.Development.json
```

**Teams app manifest (if targeting Teams):** Copy the `appManifest/` folder from [`Agents-for-net/src/samples/EmptyAgent/appManifest`](https://github.com/microsoft/Agents-for-net/tree/main/src/samples/EmptyAgent/appManifest) into your project. Then update `manifest.json`:
- Replace all `${{AAD_APP_CLIENT_ID}}` with your bot's Client ID
- Set `name.short` and `name.full` to your bot's display name

## Program.cs Structure

### Minimal setup

```csharp
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.AddAgent<MyAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

app.Run();
```

### With proactive messaging endpoints

```csharp
app.MapAgentProactiveEndpoints<MyAgent>(requireAuth: !app.Environment.IsDevelopment());
```

### With custom routes

```csharp
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
app.MapGet("/health", () => Results.Json(new { ok = true }));

app.Run();
```

### Agent class decorators

```csharp
[Agent(name: "MyAgent", description: "A helpful agent", version: "1.0")]
[AgentInterface(protocol: AgentTransportProtocol.ActivityProtocol, path: "/api/messages")]
public class MyAgent : AgentApplication
{
    // ...
}
```

### Important: AddAgent vs AddAgentApplicationOptions

- `builder.AddAgent<T>()` — registers the agent class. **Always required.**
- Do **not** call `AddAgentApplicationOptions()` separately unless you have a specific reason — `AddAgent` handles it.

### Important: MapAgentEndpoints vs MapAgentApplicationEndpoints

- `MapAgentApplicationEndpoints` — for `AgentApplication` subclasses (modern pattern)
- `MapAgentEndpoints` — for `ActivityHandler` / `TeamsActivityHandler` compat layer bots

Using the wrong one causes runtime routing failures with no clear error.

## Validating Your Configuration

### 1. Validate bot credentials (ClientId / ClientSecret / TenantId)

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
- `AADSTS700016` — `ClientId` not found in tenant (wrong ID or wrong tenant)
- `AADSTS7000215` — invalid `ClientSecret` (expired or incorrect)
- `AADSTS90002` — `TenantId` not found

### 2. Validate the agent is running and reachable

```bash
curl -s -o /dev/null -w "%{http_code}" \
  -X POST http://localhost:3978/api/messages \
  -H "Content-Type: application/json" \
  -d '{}'
```

- `401` — agent is running; JWT auth rejected the empty request (expected)
- `000` or connection refused — agent is not running or wrong port
- `200` — agent is running with auth disabled (local dev with `TokenValidation:Enabled = false`)

### 3. Validate an OAuth connection name

OAuth connection names can only be tested end-to-end via:

**Azure Portal → Your Bot Resource → Settings → OAuth Connection Settings → [your connection] → Test Connection**

## Local Testing with Agents Playground

The Agents Playground lets you test locally without deploying to Azure.

**Install:**
```bash
npm install -g agentsplayground
```

**Run against an anonymous agent:**
```bash
agentsplayground -c emulator
```

Then start your agent separately with `dotnet run`.

**With authentication:**
```bash
agentsplayground -c msteams \
  --client-id <your-app-id> \
  --client-secret <your-secret> \
  --tenant-id <your-tenant-id>
```

**Channel options** (`-c`): `msteams`, `webchat`, `directline`, `emulator`, `agents`

If the playground connects but messages don't get responses, add a fallback handler to confirm:

```csharp
OnActivity(ActivityTypes.Message, async (ctx, state, ct) =>
{
    await ctx.SendActivityAsync($"Echo: {ctx.Activity.Text}", cancellationToken: ct);
}, rank: RouteRank.Last);
```

## AgentApplication Patterns

**Routing**
```csharp
OnMessage("/cmd", handler);                                         // exact command
OnActivity(ActivityTypes.Message, handler, rank: RouteRank.Last);   // all messages (fallback)
OnConversationUpdate(ConversationUpdateEvents.MembersAdded, handler);
OnActivity(ActivityTypes.Invoke, handler);
```

**Per-route auth:**
```csharp
OnMessage("-me", OnMe, autoSignInHandlers: ["me"]);
```

**TurnState** — dot-notation keys scoped to conversation/user/temp:
```csharp
int count = state.GetValue("conversation.counter", () => 0);
state.SetValue("conversation.counter", count + 1);
state.DeleteValue("conversation.counter");
```

**Storage backends**

| Backend | Use case |
|---|---|
| `MemoryStorage` | Local dev only — not persistent |
| Cosmos DB (via `IStorage` impl) | Production |
| Blob Storage (via `IStorage` impl) | Production |

## Authorization (User Token Flow)

### Configuration in appsettings.json

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
            "Title": "Sign in with Microsoft",
            "Text": "Please sign in to continue"
          }
        }
      }
    }
  }
}
```

### Code patterns

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // Handle sign-in failure
        UserAuthorization.OnUserSignInFailure(
            async (ctx, state, handler, response, activity, ct) =>
            {
                await ctx.SendActivityAsync($"Sign-in failed: {response.Error?.Message}", cancellationToken: ct);
            });

        // Protected route — requires sign-in via "graph" handler
        OnMessage("-profile", OnProfileAsync, autoSignInHandlers: ["graph"]);

        // Fallback message handler
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        // Sign out
        OnMessage("/logout", OnLogoutAsync);
    }

    private async Task OnProfileAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        // Token is guaranteed here — route won't run until user is signed in
        string token = await UserAuthorization.GetTurnTokenAsync(ctx, "graph");
        // Use token to call Graph API
    }

    private async Task OnLogoutAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        await UserAuthorization.SignOutUserAsync(ctx, state, cancellationToken: ct);
        await ctx.SendActivityAsync("Signed out.", cancellationToken: ct);
    }
}
```

**OBO (on-behalf-of) — exchange user token for a downstream service token:**
```csharp
string newToken = await UserAuthorization.ExchangeTurnTokenAsync(
    ctx,
    "graph",
    exchangeScopes: ["https://graph.microsoft.com/.default"]
);
```

### Multiple OAuth handlers

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "auto",
      "AutoSignin": true,
      "Handlers": {
        "auto": {
          "Settings": {
            "AzureBotOAuthConnectionName": "AutoConnection"
          }
        },
        "me": {
          "Settings": {
            "AzureBotOAuthConnectionName": "MeConnection"
          }
        }
      }
    }
  }
}
```

## Cards

```csharp
using Microsoft.Agents.Core.Models;
using System.Text.Json;
```

**Adaptive Card** (from JSON):
```csharp
string cardJson = File.ReadAllText("Resources/myCard.json");
var card = new Attachment
{
    ContentType = ContentTypes.AdaptiveCard,
    Content = JsonSerializer.Deserialize<object>(cardJson)
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card), cancellationToken: ct);
```

**Hero Card:**
```csharp
var card = new HeroCard
{
    Title = "Card Title",
    Images = new List<CardImage> { new CardImage("https://example.com/image.jpg") },
    Buttons = new List<CardAction>
    {
        new CardAction(ActionTypes.OpenUrl, "Learn more", value: "https://example.com")
    }
};
await ctx.SendActivityAsync(MessageFactory.Attachment(card.ToAttachment()), cancellationToken: ct);
```

**Adaptive Card Action Execute:**
```csharp
public MyAgent(AgentApplicationOptions options) : base(options)
{
    AdaptiveCards.OnActionExecute("approve", OnApproveAsync);
    AdaptiveCards.OnActionExecute("reject", OnRejectAsync);
}

private Task<AdaptiveCardInvokeResponse> OnApproveAsync(
    ITurnContext ctx, ITurnState state, object data, CancellationToken ct)
{
    var actionData = ProtocolJsonSerializer.ToObject<MyDataModel>(data);
    return Task.FromResult(new AdaptiveCardInvokeResponse
    {
        StatusCode = 200,
        Type = "application/vnd.microsoft.card.adaptive",
        Value = BuildCard(actionData)
    });
}
```

**Adaptive Card Search (typeahead):**
```csharp
AdaptiveCards.OnSearch("myDataset", OnSearchAsync);

private Task<IList<AdaptiveCardsSearchResult>> OnSearchAsync(
    ITurnContext ctx, ITurnState state, Query<AdaptiveCardsSearchParams> query, CancellationToken ct)
{
    var results = new List<AdaptiveCardsSearchResult>
    {
        new AdaptiveCardsSearchResult("Result 1", "value1")
    };
    return Task.FromResult<IList<AdaptiveCardsSearchResult>>(results);
}
```

## Streaming

```csharp
private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
{
    try
    {
        ctx.StreamingResponse.SetFeedbackLoop(true);
        ctx.StreamingResponse.SetGeneratedByAILabel(true);
        await ctx.StreamingResponse.QueueInformativeUpdateAsync("Working on it...", ct);
        ctx.StreamingResponse.QueueTextChunk("Part 1 ");
        ctx.StreamingResponse.QueueTextChunk("Part 2");
    }
    finally
    {
        await ctx.StreamingResponse.EndStreamAsync(ct);
    }
}
```

**Streaming with Azure OpenAI:**
```csharp
private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
{
    try
    {
        await ctx.StreamingResponse.QueueInformativeUpdateAsync("Thinking...", ct);

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, cancellationToken: ct))
        {
            if (update.ContentUpdate.Count > 0 && !string.IsNullOrEmpty(update.ContentUpdate[0]?.Text))
            {
                ctx.StreamingResponse.QueueTextChunk(update.ContentUpdate[0].Text);
            }
        }
    }
    finally
    {
        await ctx.StreamingResponse.EndStreamAsync(ct);
    }
}
```

**Streaming with a final card:**
```csharp
ctx.StreamingResponse.FinalMessage = MessageFactory.Attachment(new Attachment
{
    ContentType = ContentTypes.AdaptiveCard,
    Content = cardJson
});
await ctx.StreamingResponse.EndStreamAsync(ct);
```

## Proactive Messaging

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        // Store the conversation reference for later
        string convId = await Proactive.StoreConversationAsync(ctx, ct);
        await ctx.SendActivityAsync($"Stored conversation: {convId}", cancellationToken: ct);
    }

    [ContinueConversation]
    public async Task OnProactiveAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        await ctx.SendActivityAsync("Proactive message!", cancellationToken: ct);
    }
}
```

In `Program.cs`:
```csharp
app.MapAgentProactiveEndpoints<MyAgent>(requireAuth: !app.Environment.IsDevelopment());
```

## OpenTelemetry / Observability

**Prerequisite:** Copy [`AgentOtelExtension.cs`](https://github.com/microsoft/Agents/blob/main/samples/dotnet/otel/AgentOtelExtension.cs) into your project. This provides `ConfigureOtelProviders`.

```csharp
using Otel;

builder.ConfigureOtelProviders();
```

Required packages:
```xml
<!-- OpenTelemetry packages - versions managed centrally -->
<PackageReference Include="OpenTelemetry" Version="1.*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
<!-- OpenTelemetry Exporters -->
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<!-- Azure Monitor (Application Insights) Exporter -->
<PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.*"/>
```

## Common Mistakes

**1. Wrong endpoint mapping method**

```csharp
// WRONG — for ActivityHandler compat layer only
app.MapAgentEndpoints(requireAuth: false);

// CORRECT — for AgentApplication subclasses
app.MapAgentApplicationEndpoints(requireAuth: false);
```

**2. Missing IStorage registration**

```csharp
// WRONG — no storage registered, runtime error
builder.AddAgent<MyAgent>();

// CORRECT — always register IStorage
builder.AddAgent<MyAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
```

**3. Calling AddAgentApplicationOptions separately**

```csharp
// WRONG — double registration
builder.AddAgent<MyAgent>();
builder.Services.AddAgentApplicationOptions(builder.Configuration);

// CORRECT — AddAgent handles everything
builder.AddAgent<MyAgent>();
```

**4. Wrong ConversationReference field name**

```csharp
// WRONG
var reference = new ConversationReference { Bot = new ChannelAccount(appId) };

// CORRECT
var reference = new ConversationReference { Agent = new ChannelAccount(appId) };
```

**5. Forgetting UseAuthentication/UseAuthorization**

```csharp
// WRONG — auth middleware missing
var app = builder.Build();
app.MapAgentApplicationEndpoints(requireAuth: true);

// CORRECT
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapAgentApplicationEndpoints(requireAuth: true);
```

**6. TokenValidation:Enabled left true for local dev**

If `TokenValidation:Enabled` is `true` with no valid credentials configured, every incoming request will be rejected with 401. Set to `false` for local anonymous development.

**7. Missing AspNetExtensions.cs for AddAgentAspNetAuthentication**

`AddAgentAspNetAuthentication` is NOT built into the SDK packages — it's a helper extension that must be copied into your project from the quickstart samples.

```csharp
// ERROR — CS1061: 'IServiceCollection' does not contain a definition for 'AddAgentAspNetAuthentication'
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// FIX — Copy AspNetExtensions.cs from the samples repo into your project:
// https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs
```

**8. Missing `Microsoft.Agents.Builder.State` using for ITurnState**

```csharp
// WRONG — CS0246: ITurnState could not be found
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;

// CORRECT — add the State namespace
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
```

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

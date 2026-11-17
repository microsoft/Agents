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
| `Microsoft.Agents.Extensions.MSTeams` | Teams-specific models and AgentExtension |
| `Microsoft.Agents.Extensions.A2A` | Agent-to-agent (A2A) protocol AgentExtension |
| `Microsoft.Agents.Extensions.Slack` | Slack AgentExtension |
| `Microsoft.Agents.Builder.Dialogs` | Dialog system (waterfall, prompts) |

**Minimal project requires only:** `Microsoft.Agents.Hosting.AspNetCore` + `Microsoft.Agents.Authentication.Msal`. Core builder, model, and memory-storage dependencies are transitive. Install optional AgentExtensions, dialogs, and persistent storage providers explicitly when needed.

**Always use the latest non-beta (stable) version.** Do not use `--prerelease` unless specifically needed.

Requires .NET 8+.

## Azure Resources Required

For Teams and other Azure Bot Service channels:

**Microsoft Entra App Registration**
- `ClientId` — Application (client) ID
- `ClientSecret` — Certificates & secrets
- `TenantId` — Directory (tenant) ID

**Azure Bot Resource**
- Messaging endpoint: `https://<your-host>/api/messages`
- Microsoft App ID must match `ClientId`

Anonymous local testing with Agents Playground does not require an Azure Bot. With the Quick Start hosting setup, authentication is disabled by default in the `Development` environment.

## Configuration (appsettings.json)

See [Configure authentication in your agent (.NET)](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/configure-authentication-msal?pivots=dotnet) for MSAL credential types and configuration settings. Connection settings acquire outbound tokens; the `TokenValidation` section configures validation of inbound tokens.

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

`AuthType`, `AuthorityEndpoint`, `ClientId`, `ClientSecret`, `TenantId`, `FederatedClientId`, `FederatedTokenFile`, `Scopes`, `CertSubjectName`, `CertThumbprint`, `CertStoreName`

### Local development and inbound authentication

With the Quick Start setup, `AddAgentAuthorization` skips its authentication callback in `Development` and enables it in other environments. `MapDefaultAgentEndpoints` automatically applies the matching authorization requirement. Set `ASPNETCORE_ENVIRONMENT=Development` for anonymous local testing; no token-validation configuration switch is needed.

To test authenticated requests locally, replace the authorization registration with:

```csharp
builder.AddAgentAuthorization(
    b => b.AddAgentAspNetAuthentication(),
    forceEnable: true);
```

Supply valid `TokenValidation:Audiences`, `TokenValidation:TenantId`, and connection settings when authentication is enabled. Keep production outside `Development`; do not force anonymous access in a deployed agent.

## Quick Start

Use the current [`samples/dotnet/quickstart`](https://github.com/microsoft/Agents/tree/main/samples/dotnet/quickstart) as the startup reference.

**Prerequisite:** Copy its [`AspNetExtensions.cs`](https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs) into your project. This sample helper provides `AddAgentAspNetAuthentication` for JWT token validation.

**Program.cs:**

```csharp
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();
```

The explicit `MemoryStorage` registration matches the quickstart, but is optional: the SDK falls back to `MemoryStorage` if `IStorage` is not registered. Use a persistent provider for production (see [Storage](#storage)).

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

**Teams app manifest (if targeting Teams):** Use the `teams-app-manifest` skill from `agents-sdk-common` to generate or validate a manifest against the agent's implemented Teams capabilities. For a starting example, copy [`quickstart/appManifest`](https://github.com/microsoft/Agents/tree/main/samples/dotnet/quickstart/appManifest), including the icons. Update `manifest.json`:
- Replace all `${{AAD_APP_CLIENT_ID}}` with your bot's Client ID
- Set `name.short` and `name.full` to your bot's display name
- Replace `<<BOT_DOMAIN>>` with the agent's host name and update the app metadata and URLs

## Program.cs Structure

### Minimal setup

Use the Quick Start `Program.cs` above. The responsibilities are:

| Call | Purpose |
|---|---|
| `AddAgentDefaults()` | Register the default HTTP client factory and MVC/authorization services |
| `AddAgent<MyAgent>()` | Register the agent and its application options |
| `AddAgentAuthorization(b => b.AddAgentAspNetAuthentication())` | Configure inbound JWT authentication outside Development by default |
| `UseAgents()` | Add authentication and authorization middleware in the correct order |
| `MapDefaultAgentEndpoints()` | Map `GET /` and agent message endpoints, applying the configured authorization requirement |

### With proactive messaging endpoints

```csharp
app.MapAgentProactiveEndpoints<MyAgent>(requireAuth: !app.Environment.IsDevelopment());
```

Add this before `app.Run()`. If authentication is forced on in Development, also set `requireAuth: true` for these separately mapped proactive endpoints.

### With custom routes

```csharp
var app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();
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

### Important: Default vs manual endpoint mapping

- Prefer `MapDefaultAgentEndpoints()` for `AgentApplication` subclasses with the hosting setup above.
- `MapAgentRootEndpoint()` and `MapAgentApplicationEndpoints(requireAuth: ...)` remain available for manual endpoint/authentication control.
- `MapAgentEndpoints` — for `ActivityHandler` / `TeamsActivityHandler` compat layer bots

Do not use the compat-layer mapper for an `AgentApplication` subclass.

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
  http://localhost:3978/
```

- `200` — the root health endpoint is reachable; this does not prove message processing or authentication works
- `000` or connection refused — agent is not running or wrong port

An unauthenticated `POST /api/messages` should return `401` when endpoint authorization is enabled. An empty JSON object is not a valid test activity, so do not expect a successful message response from posting `{}` with anonymous access. Use Agents Playground to verify the complete message flow.

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
[MessageRoute(rank: RouteRank.Last)]
public async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
{
    await ctx.SendActivityAsync($"Echo: {ctx.Activity.Text}", cancellationToken: ct);
}
```

## AgentApplication Patterns

### Route attributes

Prefer route attributes from `Microsoft.Agents.Builder.App`. The SDK discovers the attributed handler methods; do not also register the same route in the constructor.

| Attribute | Matches |
|---|---|
| `[MessageRoute("/cmd")]` | Exact command text (case-insensitive) |
| `[MessageRoute(rank: RouteRank.Last)]` | Fallback for unmatched messages |
| `[MembersAddedRoute]` | Members-added conversation updates |
| `[ActivityRoute(ActivityTypes.Invoke)]` | Invoke activities |

**MyAgent.cs** — the quickstart's welcome and echo behavior using route attributes:

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options) { }

    [MembersAddedRoute]
    public async Task WelcomeAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        foreach (var member in ctx.Activity.MembersAdded)
        {
            if (member.Id != ctx.Activity.Recipient.Id)
            {
                await ctx.SendActivityAsync("Hello and welcome!", cancellationToken: ct);
            }
        }
    }

    [MessageRoute(rank: RouteRank.Last)]
    public async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        await ctx.SendActivityAsync($"You said: {ctx.Activity.Text}", cancellationToken: ct);
    }
}
```

**Per-route auth:** `[MessageRoute("-me", autoSignInHandlers: "me")]` requires the configured `"me"` user sign-in handler. Attribute handler names are a delimited string, not an array. See [Authorization](#authorization-user-token-flow) for a complete example.

**TurnState** — dot-notation keys scoped to conversation/user/temp:
```csharp
int count = state.GetValue("conversation.counter", () => 0);
state.SetValue("conversation.counter", count + 1);
state.DeleteValue("conversation.counter");
```

## Storage

See [Use storage in your agent (C#)](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage?tabs=csharp).

If `IStorage` is not registered, the SDK defaults to `MemoryStorage`. This is suitable for development and testing only: state is lost on restart and is not shared across instances. Register a persistent provider for production.

| Provider | Package | Use case |
|---|---|---|
| `MemoryStorage` | `Microsoft.Agents.Storage` (transitive) | Local development and tests |
| `CosmosDbPartitionedStorage` | `Microsoft.Agents.Storage.CosmosDb` | Durable, partitioned state in Azure Cosmos DB |
| `BlobsStorage` | `Microsoft.Agents.Storage.Blobs` | Durable state in Azure Blob Storage |
| Custom `IStorage` implementation | Application-defined | Other storage services |

Choose one `IStorage` registration before `builder.Build()`. The following examples replace the quickstart's `MemoryStorage` registration; the `Storage` sections are application-defined configuration read explicitly by the code.

### Azure Cosmos DB

Install `Microsoft.Agents.Storage.CosmosDb`. Add non-secret settings to `appsettings.json`:

```json
{
  "Storage": {
    "CosmosDb": {
      "CosmosDbEndpoint": "https://<account>.documents.azure.com:443/",
      "DatabaseId": "agents",
      "ContainerId": "state"
    }
  }
}
```

In `Program.cs`:

```csharp
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.CosmosDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddSingleton<IStorage>(sp =>
{
    var options = builder.Configuration.GetSection("Storage:CosmosDb")
        .Get<CosmosDbPartitionedStorageOptions>()!;
    options.TokenCredential = sp.GetRequiredService<IConnections>()
        .GetConnection("ServiceConnection").GetTokenCredential();
    return new CosmosDbPartitionedStorage(options);
});
```

### Azure Blob Storage

Install `Microsoft.Agents.Storage.Blobs`. Add the container URI to `appsettings.json`:

```json
{
  "Storage": {
    "Blobs": {
      "ContainerUri": "https://<account>.blob.core.windows.net/agent-state"
    }
  }
}
```

In `Program.cs`:

```csharp
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using System;

builder.Services.AddSingleton<IStorage>(sp =>
{
    var credential = sp.GetRequiredService<IConnections>()
        .GetConnection("ServiceConnection").GetTokenCredential();
    return new BlobsStorage(
        new Uri(builder.Configuration["Storage:Blobs:ContainerUri"]!),
        credential);
});
```

Both examples use the identity configured in `Connections:ServiceConnection`. Grant that identity the required data-plane access to the selected store; configuring a messaging connection alone does not grant storage access. Keep secrets out of committed configuration.

## Authorization (User Token Flow)

### Configuration in appsettings.json

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "graph",
      "AutoSignIn": false,
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

Use route attributes for the protected command, sign-out command, and fallback. With `AutoSignIn: false` above, only routes declaring `autoSignInHandlers` start sign-in automatically; set it to `true` only when all turns should trigger sign-in through the default handler. User sign-in also requires the Azure Bot OAuth connection; anonymous Playground testing alone does not exercise that flow.

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        UserAuthorization.OnUserSignInFailure(
            async (ctx, state, handler, response, activity, ct) =>
            {
                await ctx.SendActivityAsync($"Sign-in failed: {response.Error?.Message}", cancellationToken: ct);
            });
    }

    [MessageRoute("-profile", autoSignInHandlers: "graph")]
    public async Task OnProfileAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        string token = await UserAuthorization.GetTurnTokenAsync(ctx, "graph", ct);
        // Use the token to call Graph; never log it or send it to the user.
    }

    [MessageRoute("/logout")]
    public async Task OnLogoutAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        await UserAuthorization.SignOutUserAsync(ctx, state, cancellationToken: ct);
        await ctx.SendActivityAsync("Signed out.", cancellationToken: ct);
    }

    [MessageRoute(rank: RouteRank.Last)]
    public async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    {
        await ctx.SendActivityAsync($"Echo: {ctx.Activity.Text}", cancellationToken: ct);
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
      "AutoSignIn": false,
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
    public MyAgent(AgentApplicationOptions options) : base(options) { }

    [MessageRoute(rank: RouteRank.Last)]
    public async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
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

Use the `agents-sdk-dotnet-otel` skill for OpenTelemetry setup, SDK trace and
metric subscriptions, safe custom instrumentation, OTLP configuration, Azure
Monitor export, validation, and the local Aspire Dashboard.

## Common Mistakes

**1. Wrong endpoint mapping method**

```csharp
// WRONG — for ActivityHandler compat layer only
app.MapAgentEndpoints(requireAuth: false);

// CORRECT — for AgentApplication subclasses
app.MapDefaultAgentEndpoints();
```

**2. Relying on MemoryStorage in production**

Omitting `IStorage` registration does not cause a registration error: the SDK defaults to `MemoryStorage`. That fallback is volatile and process-local. Register Cosmos DB, Blob Storage, or another persistent `IStorage` implementation for production; see [Storage](#storage).

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

**5. Forgetting UseAgents**

```csharp
// WRONG — auth middleware missing
var app = builder.Build();
app.MapDefaultAgentEndpoints();

// CORRECT
var app = builder.Build();
app.UseAgents();
app.MapDefaultAgentEndpoints();
```

**6. Deploying with Development or anonymous authorization**

`AddAgentAuthorization` enables authentication outside `Development` by default. Keep the authorization registration, use a production environment, and configure valid token audiences, tenant, and credentials. Use `forceEnable: true` when testing authenticated requests in Development. The `TokenValidation` section has no `Enabled` setting.

**7. Missing AspNetExtensions.cs for AddAgentAspNetAuthentication**

`AddAgentAspNetAuthentication` is NOT built into the SDK packages — it's a helper extension that must be copied into your project from the quickstart sample. `AddAgentAuthorization` itself is provided by the hosting package.

```csharp
// Copy AspNetExtensions.cs before using this callback:
// https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs
builder.AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());
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

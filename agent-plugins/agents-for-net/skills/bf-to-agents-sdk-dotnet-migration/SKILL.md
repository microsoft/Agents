---
name: bf-to-agents-sdk-dotnet-migration
description: Use when migrating a Bot Framework .NET SDK bot to Microsoft 365 Agents SDK. Triggered by projects that depend on packages: Microsoft.Bot.Builder or Microsoft.Bot.Builder.Integration.AspNet.Core that want to migrate to Agents SDK.
---

# Bot Framework to Agents SDK Migration (.NET)

## Overview

Migrates a Bot Framework SDK bot to Microsoft 365 Agents SDK using the `ActivityHandler`/`TeamsActivityHandler` compat layer — minimal code changes, original class names preserved.

**This skill stops at the compat layer.** After completing this migration, ask the user whether to also convert to `AgentApplication` (see final step).

**Agents DotNet SDK repository:** https://github.com/microsoft/agents-for-net
**Bot Framework DotNet SDK repository:** https://github.com/microsoft/botbuilder-dotnet

---

## Core Rules

- **Retain the original bot class name.** Do not rename or create a new class — renaming breaks git history.
- **Only change what is required for a clean migration.** Do not refactor, restructure, or improve beyond what is needed to compile and run on Agents SDK.

---

## Package Replacements

**Version:** Always use the latest **stable** (non-beta) version of Agents SDK packages by default.

| Remove (Bot Framework) | Add (Agents SDK) |
|------------------------|------------------|
| `Microsoft.Bot.Builder.Integration.AspNet.Core` | `Microsoft.Agents.Hosting.AspNetCore` |
| `Microsoft.Bot.Builder` | `Microsoft.Agents.Builder` |
| `Microsoft.Bot.Builder.Teams` | `Microsoft.Agents.Extensions.Teams` |
| `Microsoft.Bot.Builder.Dialogs` | `Microsoft.Agents.Builder.Dialogs` |
| `Microsoft.Bot.Connector` | `Microsoft.Agents.Connector` |
| `Microsoft.Bot.Connector.Authentication` / MSAL auth packages | `Microsoft.Agents.Authentication.Msal` |

## Deprecated Packages — No Agents SDK Equivalent

The following Bot Framework packages have **no equivalent** in the Agents SDK and are not supported. There is no recommended replacement. Remove them from the `.csproj`, but **do not attempt to migrate the code that uses them** — it will cause build errors that cannot be resolved within the Agents SDK migration. Flag this to the customer.

| Deprecated Package |
|--------------------|
| `Microsoft.Bot.Builder.AI.Luis` |
| `Microsoft.Bot.Builder.AI.Orchestrator` |
| `Microsoft.Bot.Builder.AI.QnA` |
| `Microsoft.Bot.Builder.Azure.Queues` |
| `Microsoft.Bot.Builder.Dialogs.Adaptive` |
| `Microsoft.Bot.Builder.Dialogs.Adaptive.Runtime` |
| `Microsoft.Bot.Builder.Dialogs.Adaptive.Testing` |
| `Microsoft.Bot.Builder.Dialogs.Debugging` |
| `Microsoft.Bot.Builder.LanguageGeneration` |
| `Microsoft.Bot.Builder.TemplateManager` |
| `Microsoft.Bot.Configuration` |
| `Microsoft.Bot.Connector.Streaming` |
| `Microsoft.Bot.Streaming` |
| `Microsoft.Bot.Builder.Parsers.LU` |
| `AdaptiveExpressions` |

**Migration still proceeds** — complete all other migration steps. The build errors from unmigrated code must be communicated to the customer as out-of-scope blockers.

---

## Namespace Replacements

| Old | New |
|-----|-----|
| `Microsoft.Bot.Builder` | `Microsoft.Agents.Builder` |
| `Microsoft.Bot.Builder` (ActivityHandler) | `Microsoft.Agents.Builder.Compat` |
| `Microsoft.Bot.Builder` (AutoSaveStateMiddleware) | `Microsoft.Agents.Builder.Compat` |
| `Microsoft.Bot.Builder.Teams` (TeamsActivityHandler) | `Microsoft.Agents.Extensions.Teams.Compat` |
| `Microsoft.Bot.Schema` | `Microsoft.Agents.Core.Models` |
| `Microsoft.Bot.Schema.Teams` | `Microsoft.Agents.Extensions.Teams.Models` |
| `Microsoft.Bot.Builder.Dialogs` | `Microsoft.Agents.Builder.Dialogs` |
| `Microsoft.Bot.Builder.Dialogs` (PromptValidatorContext, PromptValidatorContext\<T\>) | `Microsoft.Agents.Builder.Dialogs.Prompts` |
| `Microsoft.Bot.Builder.Integration.AspNet.Core` | `Microsoft.Agents.Hosting.AspNetCore` |
| `Microsoft.Bot.Builder.TraceExtensions` | *(remove)* — `TraceActivityAsync` is built into `ITurnContext` |

---

## Migration Steps

### Step 1: Update packages and namespaces (see tables above)

### Step 2: Keep bot class — retain original class name, only update `using` statements

`ActivityHandler` and `TeamsActivityHandler` exist unchanged in the Compat namespace. All override methods keep the same signatures. **Do not rename the class.**

```csharp
// Before
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
public class EchoBot : ActivityHandler { /* unchanged */ }

// After — only using directives change; class name stays EchoBot
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;    // ActivityHandler lives here
using Microsoft.Agents.Core.Models;        // IMessageActivity, ChannelAccount, etc.
public class EchoBot : ActivityHandler { /* unchanged */ }
```

For Teams:
```csharp
// Before
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema.Teams;
public class MyBot : TeamsActivityHandler { /* unchanged */ }

// After — class name stays MyBot
using Microsoft.Agents.Extensions.Teams.Compat;   // TeamsActivityHandler lives here
using Microsoft.Agents.Extensions.Teams.Models;    // TeamsChannelAccount, TeamInfo, etc.
public class MyBot : TeamsActivityHandler { /* unchanged */ }
```

### Step 3: State management for dialog bots

If the bot overrides `OnTurnAsync` to call `SaveChangesAsync`, both patterns work — keep whichever the bot already uses:

```csharp
// Option A: Keep existing pattern in OnTurnAsync (works as-is)
public override async Task OnTurnAsync(ITurnContext ctx, CancellationToken ct)
{
    await base.OnTurnAsync(ctx, ct);
    await ConversationState.SaveChangesAsync(ctx, false, ct);
    await UserState.SaveChangesAsync(ctx, false, ct);
}

// Option B: Use AutoSaveStateMiddleware (register in adapter setup)
// adapter.Use(new AutoSaveStateMiddleware(conversationState, userState));
```

### Step 4: New Program.cs

Delete `BotController.cs` and `AdapterWithErrorHandler.cs`. Create `Program.cs`:

```csharp
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddControllers();

// Register bot — AddAgent works because ActivityHandler implements IAgent
// DO NOT call AddAgentApplicationOptions() — that is only for AgentApplication
builder.AddAgent<MyBot>();

// Storage — always required by the Agents SDK
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Preserve all customer-specific DI registrations:
// builder.Services.AddSingleton<ConversationState>();
// builder.Services.AddSingleton<UserState>();
// builder.Services.AddSingleton<MyDialog>();
// builder.Services.AddSingleton<IMyService, MyService>();

// AddAgentAspNetAuthentication is defined in AspNetExtensions.cs — copy from any Agents SDK sample
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();

// ActivityHandler bots use MapAgentEndpoints — NOT MapAgentApplicationEndpoints
// MapAgentApplicationEndpoints only works for AgentApplication subclasses
app.MapAgentEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    app.Urls.Add("http://localhost:3978");
}

app.Run();
```

**If customer had custom logic in `BotController`**, preserve it using the process delegate:
```csharp
app.MapAgentEndpoints(
    requireAuth: !app.Environment.IsDevelopment(),
    process: async (request, response, adapter, agent, ct) =>
    {
        // Custom controller logic here
        await adapter.ProcessAsync(request, response, agent, ct);
    });
```

### Step 5 or at the very end of the migration:  Ask user if they would like to write the learnings from this migration to a markdown file they could submit to use to help us improve the skill.

---

## appsettings.json Changes

Remove Bot Framework auth config and add Agents SDK config. The shape of `Connections:ServiceConnection` depends on the value of `MicrosoftAppType` in the existing appsettings.

**In all cases:** remove `MicrosoftAppId`, `MicrosoftAppPassword`, `MicrosoftAppType`, and `MicrosoftAppTenantId` — their values are carried forward into the new structure.

---

### Case 1: `MicrosoftAppType` = `"SingleTenant"`

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{MicrosoftAppTenantId}",
        "ClientId": "{MicrosoftAppId}",
        "ClientSecret": "{MicrosoftAppPassword}",
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
    "Audiences": ["{MicrosoftAppId}"],
    "TenantId": "{MicrosoftAppTenantId}"
  }
}
```

---

### Case 2: `MicrosoftAppType` = `"UserAssignedMSI"`

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "UserManagedIdentity",
        "ClientId": "{MicrosoftAppId}",
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
    "Audiences": ["{MicrosoftAppId}"],
    "TenantId": "{MicrosoftAppTenantId}"
  }
}
```

---

### Case 3: `MicrosoftAppType` = `"MultiTenant"`, missing, or null

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/botframework.com",
        "ClientId": "{MicrosoftAppId}",
        "TenantId": "{MicrosoftAppTenantId}",
        "ClientSecret": "{MicrosoftAppPassword}",
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
    "Audiences": ["{MicrosoftAppId}"],
    "TenantId": "{MicrosoftAppTenantId}"
  }
}
```

---

### Case 4: Any other `MicrosoftAppType` value

Do **not** generate `Connections:ServiceConnection`. Complete all other migration steps, then at the end inform the user:

> "The `MicrosoftAppType` value `{value}` is not a standard type recognized by this migration. The `Connections:ServiceConnection` block was not changed — additional manual configuration will be required. See: https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/microsoft-authentication-library-configuration-options"

---

## Custom BotAdapter

If the source bot subclasses `BotAdapter`, migrate it to `ChannelAdapter` (`Microsoft.Agents.Builder`).

### Class hierarchy to choose from

| Use when | Subclass |
|----------|----------|
| Custom transport / non-Azure channel | `ChannelAdapter` (abstract, minimal pipeline) |
| Azure Bot Service connectivity with custom behavior | `CloudAdapter` (`Microsoft.Agents.Hosting.AspNetCore`) |

Most custom `BotAdapter` subclasses fall into the **CloudAdapter** case. Prefer that unless the adapter is implementing a completely custom transport.

### API mapping

| Bot Framework `BotAdapter` | Agents SDK |
|---------------------------|------------|
| `using Microsoft.Bot.Builder;` | `using Microsoft.Agents.Builder;` |
| `: BotAdapter` | `: ChannelAdapter` or `: CloudAdapter` |
| `Activity[]` in `SendActivitiesAsync` | `IActivity[]` in `SendActivitiesAsync` |
| `BotCallbackHandler` callback delegate | `AgentCallbackHandler` |
| `abstract UpdateActivityAsync(...)` | `virtual UpdateActivityAsync(...)` (throws `NotImplementedException` if not overridden) |
| `abstract DeleteActivityAsync(...)` | `virtual DeleteActivityAsync(...)` (throws `NotImplementedException` if not overridden) |
| `protected RunPipelineAsync(ctx, BotCallbackHandler, ct)` | `protected RunPipelineAsync(ctx, AgentCallbackHandler, ct)` |

### Registration

Register the custom adapter via the two-type overload of `AddAgent`. Note `AddAgent<TAgent, TAdapter>` requires `TAdapter : CloudAdapter`:

```csharp
builder.AddAgent<MyBot, MyCustomAdapter>();
```

If the custom adapter subclasses `ChannelAdapter` directly (not `CloudAdapter`), register manually:

```csharp
builder.AddAgentCore();  // registers IConnections, IChannelServiceClientFactory
builder.Services.AddSingleton<IChannelAdapter, MyCustomAdapter>();
builder.Services.AddSingleton<IAgentHttpAdapter>(sp => (IAgentHttpAdapter)sp.GetRequiredService<IChannelAdapter>());
builder.Services.AddTransient<IAgent, MyAgent>();
```

---

## Custom IMiddleware

If the source bot implements `Microsoft.Bot.Builder.IMiddleware`, retain the class and update its namespaces:

| Old | New |
|-----|-----|
| `Microsoft.Bot.Builder.IMiddleware` | `Microsoft.Agents.Builder.IMiddleware` |
| `Microsoft.Bot.Builder.ITurnContext` | `Microsoft.Agents.Builder.ITurnContext` |
| `Microsoft.Bot.Builder.NextDelegate` | `Microsoft.Agents.Builder.NextDelegate` |

The `OnTurnAsync(ITurnContext, NextDelegate, CancellationToken)` signature is unchanged.

Register custom middleware via a `CloudAdapter` subclass. Delete `AdapterWithErrorHandler.cs` only if it has no custom middleware — if it does, rename it or keep it as a custom adapter:

```csharp
// CustomAdapter.cs
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Logging;

public class CustomAdapter : CloudAdapter
{
    public CustomAdapter(
        IChannelServiceClientFactory channelServiceClientFactory,
        IActivityTaskQueue activityTaskQueue,
        ILogger<CloudAdapter> logger = null)
        : base(channelServiceClientFactory, activityTaskQueue, logger: logger)
    {
        Use(new MyCustomMiddleware());
        // Use(new AnotherMiddleware(...));
    }
}
```

Register the custom adapter in `Program.cs`:

```csharp
builder.AddAgent<MyBot, CustomAdapter>();
```

---

## Files to Add

Every migrated project needs `AspNetExtensions.cs` — the `AddAgentAspNetAuthentication()` extension method is **not in any NuGet package**; it is a sample-provided file. Copy it from the Agents SDK quickstart sample (https://github.com/microsoft/Agents/blob/main/samples/dotnet/quickstart/AspNetExtensions.cs).

---

## Files to Delete

Always delete these from the source project:
- `AdapterWithErrorHandler.cs` — delete if it only has `OnTurnError` logic (Agents SDK handles errors internally); **retain and rename** if it contains custom `Use()` middleware registrations (see Custom IMiddleware section above)
- `BotController.cs` (unless it has custom logic — see preservation note above)
- Any `Startup.cs` (merge into `Program.cs`)

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Renamed the bot class or created a new class | Restore the original class name — renaming breaks git history |
| Changed more than needed (refactored, restructured) | Revert — only change what is required to compile and run |
| Called `AddAgentApplicationOptions()` for ActivityHandler bot | Remove — only for AgentApplication |
| Used `MapAgentApplicationEndpoints()` for ActivityHandler bot | Use `MapAgentEndpoints()` — former only works for AgentApplication |
| Left `services.AddTransient<IBot, MyBot>()` | Replace with `builder.AddAgent<MyBot>()` |
| Left `services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>()` | Delete — handled by `AddAgent<>()` |
| Left `ConfigurationBotFrameworkAuthentication` | Delete — use `AddAgentAspNetAuthentication()` |
| Missing `IStorage` registration | Add `builder.Services.AddSingleton<IStorage, MemoryStorage>();` — always required, not just for dialog bots |
| Left old Bot Framework appsettings (`MicrosoftAppId` etc.) | Replace with `Connections` + `TokenValidation` — shape depends on `MicrosoftAppType` (see appsettings.json Changes section) |
| Used the wrong `Connections:ServiceConnection` shape | Check `MicrosoftAppType`: `SingleTenant`→ClientSecret+tenant authority; `UserAssignedMSI`→UserManagedIdentity (no secret); `MultiTenant`/missing→ClientSecret+botframework.com authority |
| Left `BotAdapter` as base class of custom adapter | Change to `ChannelAdapter` or `CloudAdapter`; update `Activity[]` → `IActivity[]`, `BotCallbackHandler` → `AgentCallbackHandler` |
| Deleted `AdapterWithErrorHandler.cs` that contained custom `Use()` calls | Retain it (renamed if needed) as a `CloudAdapter` subclass; register via `builder.AddAgent<TBot, TAdapter>()` |
| Left `Microsoft.Bot.Builder.IMiddleware` namespace in custom middleware | Update to `Microsoft.Agents.Builder.IMiddleware` |
| Attempted to migrate code using a deprecated package (Luis, QnA, Adaptive, etc.) | Stop — no Agents SDK equivalent exists. Remove the package reference, leave the code in place, and flag build errors to the customer |
| Forgot to add `AspNetExtensions.cs` | Copy from any Agents SDK sample — `AddAgentAspNetAuthentication` is not in any NuGet package |
| Left `conversationState.DeleteAsync(...)` in adapter | Rename to `conversationState.DeleteStateAsync(...)` — method was renamed in Agents SDK |
| New `Program.cs` fails to compile (WebApplication, AddSingleton, IsDevelopment not found) | BF projects don't have `<ImplicitUsings>enable</ImplicitUsings>` — add explicit `using` for `Microsoft.AspNetCore.Builder`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting` |

---

## After Migration: Optional AgentApplication Upgrade

Once the compat-layer migration is complete and the build is clean, ask the user:

> "The bot is now running on Agents SDK using the ActivityHandler compat layer. Would you like to also migrate to the modern `AgentApplication` routing pattern? This involves converting the bot class to subclass `AgentApplication` and updating Program.cs — it is handled by the `agents-sdk-dotnet-activityhandler-migration` skill."

If yes, invoke `agents-sdk-dotnet-activityhandler-migration`.
If no, migration is complete.

---
name: teams-sdk-to-agents-sdk-dotnet-migration
description: Use when migrating a customer's Teams SDK (teams.net) bot to the Microsoft 365 Agents SDK in .NET. Triggered by projects that depend on Microsoft.Teams.Plugins.AspNetCore, Microsoft.Teams.Apps, or Microsoft.Teams.Api and call builder.AddTeams() / app.UseTeams().
---

# Teams SDK (teams.net) to Agents SDK Migration (.NET)

## Overview

When an automation caller supplies validation tools instead of shell access, use those
tools to perform the build, tests, and startup checks required below. A current passing
tool result is equivalent evidence; do not request unavailable shell access or claim
validation from source inspection alone. Follow the caller's read-only role when reviewing.

Migrates a **Teams SDK** (`teams.net`, https://github.com/microsoft/teams.net) bot to a Microsoft 365
Agents SDK **`AgentApplication`**. The Teams SDK registers fluent event delegates
(`teamsApp.OnMessage(async context => ...)`) on an `IContext<T>`; the Agents SDK routes activities to
handler methods on an `AgentApplication` subclass using `ITurnContext`.

**This is NOT the Bot Framework migration.** If the project depends on `Microsoft.Bot.Builder*`, use
`bf-to-agents-sdk-dotnet-migration` instead. Trigger this skill only for `teams.net` projects
(`Microsoft.Teams.Plugins.AspNetCore`, `Microsoft.Teams.Apps`, `Microsoft.Teams.Api`).

**Agents SDK repository:** https://github.com/microsoft/agents-for-net
**Teams SDK repository:** https://github.com/microsoft/teams.net

---

## Core Rules

- **Preserve behavior.** Keep the same commands, text matching, and responses except for user-facing references to the migrated runtime, which should say "agent" instead of "bot". Make other response changes only when required to run on the Agents SDK.
- Describe the migrated runtime as an agent in README text, catalog descriptions, and user-facing messages. Keep platform terms such as Azure Bot, bot ID, and manifest `bots[]`, and preserve source-derived paths and identifiers unless the caller requests a rename.
- After source migration is stable, use the standalone `teams-app-manifest` skill to generate, complete,
  or audit the Teams app manifest from source evidence and explicit product intent. Do not embed manifest
  feature rules in this migration skill.
- **Use stable Agents SDK packages.** A caller's minimum Agents SDK version is a floor, not the
  version to copy into the project. In automated sample sync, use the `selectedAgentsSdkVersion`
  in the frozen sync context. Respect a customer's explicitly selected release line. Otherwise,
  read the [Agents SDK release history JSON](https://api.github.com/repos/microsoft/Agents-for-net/releases?per_page=100)
  (follow later pages when needed) and select the newest stable release within the permitted major,
  then confirm a common major.minor release line for `Microsoft.Agents.Hosting.AspNetCore`,
  `Microsoft.Agents.Authentication.Msal`, and `Microsoft.Agents.Extensions.MSTeams` from the
  [Hosting](https://api.nuget.org/v3-flatcontainer/microsoft.agents.hosting.aspnetcore/index.json),
  [Authentication](https://api.nuget.org/v3-flatcontainer/microsoft.agents.authentication.msal/index.json),
  and [MSTeams](https://api.nuget.org/v3-flatcontainer/microsoft.agents.extensions.msteams/index.json)
  JSON version feeds.
  Use the newest compatible stable line available for all three, without assuming their patch
  numbers match. Do not select a prerelease or a new major version without explicit policy.
- If the project defines the `AgentApplication` during DI (typically in `Program.cs`), ask the customer
  whether to keep it inline or move it to an `AgentApplication` subclass with Teams route attributes.
  In the automated Teams sample sync, do not ask a user. Apply an applicable migration policy or return
  `needs-policy` with evidence and a recommended policy.
- If creating a subclass, always use `[TeamsExtension]`, mark it `partial`, use Teams route attributes,
  and take `ITeamsTurnContext` in route handlers.
- After restoring packages, verify route attributes, handler delegate signatures, payload models, and
  response types against the installed configured-version XML documentation or current Agents SDK source. Do
  not infer an API shape from a similarly named Teams SDK API.
- Preserve the original registration and `if`/`else` precedence. Use explicit route `rank` values
  whenever multiple text or invoke routes can match the same activity.
- Prefer `SendActivityAsync("text", cancellationToken: ct)` for plain text. When a reply needs entities, attachments, or other activity fields, build an activity with `Activity.CreateMessageActivity()` and fluent methods such as `.WithText(...)`, then set the extra fields. Keep a factory when it adds value for a structured response.
- Use the agent's logger for diagnostics instead of `Console.WriteLine`; pass exceptions to structured logging rather than logging only their messages.

---

## Package Replacements

Out-of-repo samples use **PackageReferences** (not ProjectReferences). Replace the single Teams SDK
package with the Agents SDK packages, using the selected major.minor release line for all three.
Replace `RESOLVED_VERSION` below with that line's floating version (for example, `major.minor.*`)
before writing the project file:

| Remove (Teams SDK)                     | Add (Agents SDK)                                                          |
|----------------------------------------|--------------------------------------------------------------------------|
| `Microsoft.Teams.Plugins.AspNetCore`   | `Microsoft.Agents.Hosting.AspNetCore` + `Microsoft.Agents.Authentication.Msal` |
| *(Teams routing surface)*              | `Microsoft.Agents.Extensions.MSTeams` |
| `Microsoft.Teams.Cards` *(if used)*     | `Microsoft.Teams.Cards` *(kept — Agents MSTeams extension reuses it)*     |

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="RESOLVED_VERSION" />
  <PackageReference Include="Microsoft.Agents.Authentication.Msal" Version="RESOLVED_VERSION" />
  <PackageReference Include="Microsoft.Agents.Extensions.MSTeams" Version="RESOLVED_VERSION" />
</ItemGroup>
```

`Microsoft.Agents.Hosting.AspNetCore` transitively brings `Microsoft.Agents.Builder`,
`Microsoft.Agents.Core`, `Microsoft.Agents.Connector`, and `Microsoft.Agents.Storage`. The
`[TeamsExtension]` source generator ships inside `Microsoft.Agents.Core` (analyzers folder), so NuGet
consumers get it transitively — no explicit analyzer `PackageReference` is needed.

**TargetFramework:** use the repository's configured target framework, currently `net10.0`. Set
`<TargetFramework>net10.0</TargetFramework>`. Set `<ImplicitUsings>disable</ImplicitUsings>` and add
explicit `using` directives (matches Agents SDK sample convention).

---

## Namespace Replacements

| Teams SDK (`teams.net`)                          | Agents SDK                                       |
|--------------------------------------------------|--------------------------------------------------|
| `Microsoft.Teams.Plugins.AspNetCore.Extensions`  | `Microsoft.Agents.Hosting.AspNetCore`            |
| `Microsoft.Teams.Apps`                           | `Microsoft.Agents.Builder`, `Microsoft.Agents.Builder.App` |
| `Microsoft.Teams.Apps.Activities`                | `Microsoft.Agents.Builder.App` (routing)         |
| `Microsoft.Teams.Api.Activities`                 | `Microsoft.Agents.Core.Models`                   |
| `Microsoft.Teams.Api` (models)                   | `Microsoft.Agents.Core.Models`                   |
| *(Teams-specific)* `Microsoft.Teams.Apps.Activities.Events` etc. | `Microsoft.Agents.Extensions.MSTeams.*` |

---

## Migration Steps

### Step 1 — Scaffold the copy

Create `agentsdk-<name>/dotnet/agentsdk-<name>/` mirroring the customer's source layout. Files:

- `<Project>.csproj` — SDK.Web, `net10.0`, PackageReferences above.
- `Program.cs` — Agents SDK host (Step 3).
- `<Name>Agent.cs` — the `AgentApplication` subclass (Step 4).
- `AspNetExtensions.cs` — **required.** `AddAgentAspNetAuthentication()` is *not* in any NuGet package;
  copy it verbatim from an Agents SDK sample
  (`src/samples/EmptyAgent/AspNetExtensions.cs` in agents-for-net).
- `appsettings.json` — non-secret Agents SDK config (Step 5); keep local credentials in an ignored `appsettings.Development.json` or environment variables.
- `Properties/launchSettings.json` — `applicationUrl: http://localhost:3978`.
- `README.md` — explain the migrated Agents SDK application and its customer-specific setup.

### Step 2 — Map the surface area to route attributes

Scan the Teams SDK `Program.cs` (or handler files) for the `teamsApp.On*` registrations and map each to
its Teams route attribute (see the tables below). The agent is **always** a `[TeamsExtension] partial`
class — `OnMessage` → `[TeamsMessageRoute]`, `OnConversationUpdate` → `[TeamsMembersAddedRoute]` /
`[TeamsMembersRemovedRoute]`, plus any Teams-specific `On*`.

### Step 3 — Program.cs

The Teams SDK bootstrap becomes the Agents SDK host:

```csharp
// Teams SDK
var builder = WebApplication.CreateBuilder(args);
builder.AddTeams();
var webApp = builder.Build();
var teamsApp = webApp.UseTeams(true);
// ... teamsApp.On*(...) registrations ...
webApp.Run();
```

```csharp
// Agents SDK
using MyAgentNamespace;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();
app.UseAgents();
app.MapDefaultAgentEndpoints();   // GET "/" + POST /api/messages
app.Run();
```

`AddAgentDefaults()` registers `MemoryStorage`, default options, and the standard HTTP client
services. Do not add `builder.Services.AddHttpClient()` to `Program.cs`. Inline `teamsApp.On*`
lambdas move to handler methods on the agent class (Step 4).

### Step 4 — The agent class

Convert inline delegates into a `[TeamsExtension] partial AgentApplication` subclass. Route each
command with a Teams route attribute; handlers take **`ITeamsTurnContext`**.

This context rule applies to route attributes from `Microsoft.Agents.Extensions.MSTeams.App`.
Base Agents SDK routes must use their documented delegate signature. For example,
`[ActionExecuteRoute]` uses `ITurnContext`, not `ITeamsTurnContext`.

**Text dispatch semantics** (`[TeamsMessageRoute]`):
- `[TeamsMessageRoute("whoami")]` — exact text match, **case-insensitive**.
- `[TeamsMessageRoute(textRegex: "(?i)whoami")]` — regex match; use this to reproduce the Teams SDK's
  `text.Contains(...)` dispatch (add `(?i)` for case-insensitivity).
- `[TeamsMessageRoute]` (no argument) — catch-all; matches any message and defaults to `RouteRank.Last`
  so specific-text routes win.
- When several `Contains` branches could overlap, set `rank:` (lower = higher priority) to preserve the
  original `if/else` precedence.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;              // Activity, Mention, ActivityTypes
using Microsoft.Agents.Extensions.MSTeams;       // [TeamsExtension], ITeamsTurnContext
using Microsoft.Agents.Extensions.MSTeams.App;   // [TeamsMessageRoute], [TeamsMembersAddedRoute]

namespace MyAgentNamespace;

[TeamsExtension]
public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [TeamsMembersAddedRoute]
    public async Task OnMembersAddedAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        foreach (var member in turnContext.Activity.MembersAdded)
            if (member.Id == turnContext.Activity.Recipient.Id)
                await turnContext.SendActivityAsync("Welcome!", cancellationToken: ct);
    }

    [TeamsMessageRoute(textRegex: "(?i)whoami", rank: 11)]
    public async Task WhoAmIAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken ct)
        => await turnContext.SendActivityAsync($"You are: {turnContext.Activity.From.Name}", cancellationToken: ct);

    [TeamsMessageRoute]  // catch-all
    public async Task DefaultAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken ct)
        => await turnContext.SendActivityAsync("Welcome!", cancellationToken: ct);
}
```

`AddAgentDefaults()` registers `MemoryStorage` and default options — no explicit `IStorage` needed for a
basic agent. Inline `teamsApp.On*` lambdas move to route-attributed methods on the agent class.

### Step 5 — application settings

Teams SDK reads flat `Teams:ClientId/ClientSecret/TenantId` (or `CLIENT_ID`/`CLIENT_SECRET` env vars).
Agents SDK uses `Connections` + `TokenValidation` (+ optional `AgentApplication`). Carry the values
across using the customer's authentication type (default below is a SingleTenant Azure Bot with
ClientSecret). Keep the checked-in `appsettings.json` free of real credentials. Put local values in
an ignored `appsettings.Development.json` or environment variables, and show that pattern in the
README. A checked-in `appsettings.json` can use placeholders for identity values while keeping the
secret unset:

```json
{
  "TokenValidation": {
    "Audiences": [ "{{ClientId}}" ],
    "TenantId": "{{TenantId}}"
  },
  "OutboundHostValidator": {
    "Enabled": false,
    "IncludeDefaultMicrosoftHosts": true,
    "Hosts": []
  },
  "AgentApplication": {
    "StartTypingTimer": false,
    "RemoveRecipientMention": false,
    "NormalizeMentions": false
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "{{ClientId}}",
        "ClientSecret": null,
        "Scopes": [ "https://api.botframework.com/.default" ]
      }
    }
  },
  "ConnectionsMap": [ { "ServiceUrl": "*", "Connection": "ServiceConnection" } ]
}
```

For local development, put the client ID, tenant ID, and client secret in
`appsettings.Development.json` using the same nested keys. The file can contain only the values it
overrides. Ensure it is ignored by Git and run with the `Development` environment. An environment
override can set the secret with
`Connections__ServiceConnection__Settings__ClientSecret`. Keep identity values consistent between
`TokenValidation`, the service connection, and any separate Graph configuration.

For other Azure Bot types (UserAssignedMSI, MultiTenant) see the `bf-to-agents-sdk-dotnet-migration`
skill's appsettings cases and https://aka.ms/AgentsSDK-DotNetMSALAuth.

### Step 6 — Generate or audit the Teams app manifest

Use the `teams-app-manifest` skill with the migrated source root, original manifest if present, intended
Teams scopes, distribution target, and repository placeholder convention.

- Preserve an existing manifest unless a verified migrated capability requires a change.
- If the source sample has no manifest, generate one only when all required product decisions are known.
- Return manifest evidence in the transient structured `manifestReport`. Do not create
  `manifest-evidence.md` in the automated Teams sample sync.
- Report portal, bot registration, Entra, permission, and event-subscription work separately.
- Stop on `needs-input`; do not guess scopes, domains, identity values, or permissions. In the automated
  Teams sample sync, report this as `needs-policy` and leave the blocked behavior unchanged.

### Step 7 — Verify

Run `dotnet build` with 0 warnings/errors, then run the migrated customer application and confirm it
listens on `http://localhost:3978` and `GET /` returns 200.

Also run the schema, package, evidence, and external-configuration checks required by the
`teams-app-manifest` skill when a manifest is present or generated.

---

## API Mapping (base conversational features)

| Teams SDK (`teams.net`)                                   | Agents SDK                                                       |
|-----------------------------------------------------------|-----------------------------------------------------------------|
| `IContext<T> context`                                     | `ITurnContext turnContext` (+ `ITurnState`, `CancellationToken`) |
| `context.Activity`                                        | `turnContext.Activity`                                           |
| `context.Activity.Text`                                   | `turnContext.Activity.Text`                                      |
| `context.Activity.From` / `.From.Name`                    | `turnContext.Activity.From` / `.From.Name`                      |
| `context.Activity.Recipient?.Id`                          | `turnContext.Activity.Recipient.Id`                             |
| `context.Activity.MembersAdded`                           | `turnContext.Activity.MembersAdded`                             |
| `await context.Send("text")`                              | `await turnContext.SendActivityAsync("text", cancellationToken: ct)` |
| `await context.Send(messageActivity)`                     | `await turnContext.SendActivityAsync(activity, ct)`            |
| `await context.Send(adaptiveCard)`                        | `await turnContext.SendActivityAsync(MessageFactory.Attachment(new Attachment(ContentTypes.AdaptiveCard, card)), ct)` |
| `new MessageActivity().WithText("t")`                     | `Activity.CreateMessageActivity().WithText("t")` |
| `.AddMention(member, addText: false)`                     | build a `Mention` entity + assign `activity.Entities = [mention]` |
| `context.Api` (Teams REST client)                         | `ITeamsTurnContext.Client` (preferred) or `TeamsExtension.GetTeamsClient(turnContext)` (needs `[TeamsExtension]`) |

### Mention example

```csharp
// Teams SDK
var activity = new MessageActivity()
    .WithText($"Hello <at>{member.Name}</at>")
    .AddMention(member, addText: false);
await context.Send(activity);
```
```csharp
// Agents SDK
var mention = new Mention { Mentioned = member, Text = $"<at>{member.Name}</at>" };
var activity = Activity.CreateMessageActivity().WithText($"Hello {mention.Text}");
activity.Entities = [mention];
await turnContext.SendActivityAsync(activity, ct);
```

---

## Teams-specific event mapping (requires `[TeamsExtension]`)

Mark the class `[TeamsExtension] public partial class MyAgent(...) : AgentApplication(...)`, add the
`Microsoft.Agents.Extensions.MSTeams` package, and use **`ITeamsTurnContext`** (not `ITurnContext`) in
Teams route handlers. Each Teams SDK `teamsApp.On*` maps to a route attribute:

| Teams SDK event                          | Agents SDK route attribute                                             |
|------------------------------------------|-----------------------------------------------------------------------|
| `OnMessage`                              | `[TeamsMessageRoute]` or `[TeamsMessageRoute("text")]` or `AgentApplication.OnTeamsMessage` |
| `OnConversationUpdate`                   | `[TeamsConversationUpdateRoute]` / `[TeamsMembersAddedRoute]` / `[TeamsMembersRemovedRoute]` |
| `OnInstall`                              | `[TeamsInstallationUpdateRoute]` plus an `InstallationUpdateActionTypes.Add` guard |
| `OnInstallUpdate`                        | `[TeamsInstallationUpdateRoute]`                                      |
| `OnMeetingStart`                         | `[TeamsMeetingStartRoute]`                                            |
| `OnMeetingEnd`                           | `[TeamsMeetingEndRoute]`                                              |
| `OnMeetingJoin`                          | `[TeamsMeetingParticipantsJoinRoute]`                                |
| `OnMeetingLeave`                         | `[TeamsMeetingParticipantsLeaveRoute]`                               |
| `OnFileConsent` (Accept branch)          | `[TeamsFileConsentAcceptRoute]`                                      |
| `OnFileConsent` (Decline branch)         | `[TeamsFileConsentDeclineRoute]`                                     |
| `OnTaskFetch`                            | `[TeamsTaskFetchRoute("value")]` → returns `Microsoft.Teams.Api.TaskModules.Response` |
| `OnTaskSubmit`                           | `[TeamsTaskSubmitRoute("value")]` → returns `Microsoft.Teams.Api.TaskModules.Response` |
| `OnMessageExtensionQuery`                | `[TeamsQueryRoute("commandId")]`                                     |
| `OnMessageExtensionSubmitAction`         | `[TeamsSubmitActionRoute("commandId")]`                              |
| `OnMessageExtensionFetchTask`            | `[TeamsFetchActionRoute("commandId")]`                              |
| `OnMessageExtensionQueryLink`            | `[TeamsQueryLinkRoute]` / `[TeamsAnonQueryLinkRoute]`               |
| `OnMessageExtensionSelectItem`           | `[TeamsSelectItemRoute]`                                             |
| `OnAdaptiveCardAction` / `Action.Execute` | `[ActionExecuteRoute]` returning `AdaptiveCardInvokeResponse`         |
| Teams card button clicked invoke         | `[TeamsCardButtonClickedRoute]`                                       |
| Channel created/renamed/deleted/etc.     | `[TeamsChannelCreatedRoute]`, `[TeamsChannelRenamedRoute]`, `[TeamsChannelDeletedRoute]`, ... |
| Team renamed/archived/deleted/etc.       | `[TeamsTeamRenamedRoute]`, `[TeamsTeamArchivedRoute]`, `[TeamsTeamDeletedRoute]`, ... |
| Message edit/delete/undelete/read        | `[TeamsMessageEditRoute]`, `[TeamsMessageDeleteRoute]`, `[TeamsMessageUndeleteRoute]`, `[TeamsReadReceiptRoute]` |
| Config fetch/submit                      | `[TeamsConfigFetchRoute]`, `[TeamsConfigSubmitRoute]`               |

Teams route handler signature (note `ITeamsTurnContext`, and the extra deserialized payload
parameter whose type depends on the event):

```csharp
// Meeting start/end payload is Microsoft.Teams.Api.Meetings.MeetingDetails
[TeamsMeetingStartRoute]
public async Task OnMeetingStartAsync(ITeamsTurnContext turnContext, ITurnState turnState,
    Microsoft.Teams.Api.Meetings.MeetingDetails meeting, CancellationToken ct) { ... }

// Participant join/leave payload is Microsoft.Agents.Extensions.MSTeams.Models.MeetingParticipantsEventDetails
[TeamsMeetingParticipantsJoinRoute]
public async Task OnJoinAsync(ITeamsTurnContext turnContext, ITurnState turnState,
    MeetingParticipantsEventDetails participants, CancellationToken ct) { ... }
```

`teamsApp.OnInstall(...)` represents only a new installation. Since
`[TeamsInstallationUpdateRoute]` also receives remove and upgrade actions, preserve `OnInstall`
semantics with an explicit guard:

```csharp
if (turnContext.Activity.Action != InstallationUpdateActionTypes.Add)
{
    return;
}
```

### Adaptive Card `Action.Execute`

Do not route `Action.Execute` through a generic `[TeamsActivityRoute("invoke")]`. Teams expects an
adaptive-card invoke response, and a fire-and-forget `Task` handler loses that response.

```csharp
using Microsoft.Agents.Builder.App.AdaptiveCards;

[ActionExecuteRoute("submit_name")]
public Task<AdaptiveCardInvokeResponse> OnSubmitNameAsync(
    ITurnContext turnContext,
    ITurnState turnState,
    object data,
    CancellationToken cancellationToken)
{
    return Task.FromResult(AdaptiveCardInvokeResponseFactory.Message("Submitted."));
}
```

The route's `verb` matches the Adaptive Card `Action.Execute` verb. Deserialize `data` using the
actual card payload shape, preserve the original action behavior, and return an
`AdaptiveCardInvokeResponse` for every path.

### AI-generated content, sensitivity, and citations

Do not approximate Teams SDK AI helpers with `ChannelData`, generic `Entity` objects, or
stringified JSON. Use the Agents SDK models so Teams receives the required schema:

```csharp
var aiEntity = new AIEntity
{
    AdditionalType = [AIEntity.AdditionalTypeAIGeneratedContent],
    UsageInfo = new SensitivityUsageInfo
    {
        Name = "Confidential",
        Description = "Handle according to your organization's policy."
    },
    Citation =
    [
        new ClientCitation(
            1,
            "Source title",
            "Source summary",
            string.Empty,
            ["keyword"],
            "https://example.com/source",
            ClientCitationIconName.MicrosoftWord)
    ]
};

activity.Entities = [aiEntity];
```

- `AddAIGenerated()` maps to `AIEntity.AdditionalTypeAIGeneratedContent`.
- Sensitivity metadata maps to `AIEntity.UsageInfo` with `SensitivityUsageInfo`.
- Citations map to `AIEntity.Citation` with `ClientCitation`.
- Feedback buttons use the current Teams feedback-loop helper and
  `[TeamsFeedbackLoopRoute]` for submitted feedback.

### Long-running invoke work

File consent and other invoke handlers should acknowledge promptly. Do not keep the incoming HTTP
request open for a long upload, and do not use its cancellation token for work that must continue
after the invoke completes.

- Capture the conversation information and immutable payload needed by the operation.
- Queue the work through a background service.
- Send completion through an Agents SDK proactive continuation.
- Do not retain or use the original `ITurnContext` after the handler returns.
- Do not use untracked `Task.Run` as a substitute for a background service.

Meeting property renames vs. the Teams SDK event `Value`: `.StartTime` → `.ScheduledStartTime`,
`.EndTime` → `.ScheduledEndTime` (`.Title`, `.JoinUrl`, `.Id`, `.MSGraphResourceId` are unchanged).

Cards: Teams SDK `Microsoft.Teams.Cards.AdaptiveCard` types are reused by the Agents MSTeams extension —
keep `Microsoft.Teams.Cards` and send via `MessageFactory.Attachment(new Attachment(ContentTypes.AdaptiveCard, card))`.

Outbound Teams REST (`context.Api`) → `ITeamsTurnContext.Client` or `TeamsExtension.GetTeamsClient(turnContext)` (e.g.
`context.Api.Meetings.GetByIdAsync(id)` → `context.Client.Meetings.GetByIdAsync(id)`).
For **turn/user (delegated/OBO)** Graph calls use `TeamsExtension.GetGraphClient(turnContext)`. For
**app-only** Graph calls (e.g. meeting transcripts via `OnlineMeetingTranscript.Read.All`), keep the
original independent app-only client — `Azure.Identity` + `Microsoft.Graph` with a
`ClientSecretCredential` built from config — rather than `GetGraphClient`. (There is no `TeamsInfo`
static helper in the Agents SDK.)

Gotcha: any external client created **eagerly** in the agent constructor (e.g. a
`ClientSecretCredential`) must guard against blank/placeholder config, or the host crashes at
startup. Default such config values to empty and skip client creation when unset (wrap creation in
try/catch on `ArgumentException` as a backstop).

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Used mismatched Agents package release lines | Resolve the newest compatible stable line shared by Hosting, Authentication, and MSTeams; respect the caller's minimum and frozen sync target |
| Retained `Microsoft.Agents.Extensions.MSTeams` `1.0.43-beta` | Replace it with the same current version used by the other Agents SDK packages |
| Used base `AgentApplication` + `OnActivity`/constructor routes instead of `[TeamsExtension]` + route attributes | Always mark the class `[TeamsExtension] partial` and use `[TeamsMessageRoute]` / `[TeamsMembersAddedRoute]` / etc. |
| Used `ITurnContext` in an MSTeams route handler | Route attributes from `Microsoft.Agents.Extensions.MSTeams.App` require `ITeamsTurnContext`; base routes such as `[ActionExecuteRoute]` use their documented `ITurnContext` signature |
| Routed `Action.Execute` through a generic invoke route | Use `[ActionExecuteRoute]` and return `AdaptiveCardInvokeResponseFactory` responses |
| Built AI metadata with `ChannelData`, generic entities, or JSON strings | Use `AIEntity`, `SensitivityUsageInfo`, and `ClientCitation` |
| Ran a file upload inline with the invoke cancellation token | Acknowledge promptly, queue background work, and send completion through proactive continuation |
| Treated every installation update as an install | For migrated `OnInstall`, handle only `InstallationUpdateActionTypes.Add` |
| Forgot `AspNetExtensions.cs` | Copy it from an Agents SDK sample — `AddAgentAspNetAuthentication` is not in any NuGet package |
| Left `builder.AddTeams()` / `webApp.UseTeams()` | Replace with `AddAgentDefaults().AddAgent<T>()...` and `app.UseAgents()` + `app.MapDefaultAgentEndpoints()` |
| Kept `context.Send(...)` / `IContext<T>` | Use `turnContext.SendActivityAsync(...)` / `ITurnContext` |
| Kept flat `Teams:*` auth config | Replace with `Connections` + `TokenValidation` (SingleTenant ClientSecret by default) |
| Left `TargetFramework` at `net10.0` with `ImplicitUsings` | Use `net8.0`, disable implicit usings, add explicit `using`s |
| `Missing MessageFactory / ActivityTypes` compile errors | Add `using Microsoft.Agents.Builder;` and `using Microsoft.Agents.Core.Models;` |

---

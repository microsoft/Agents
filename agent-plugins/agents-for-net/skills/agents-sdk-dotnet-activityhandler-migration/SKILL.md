---
name: agents-sdk-dotnet-activityhandler-migration
description: Use when migrating a Microsoft 365 Agents SDK agent that uses ActivityHandler or TeamsActivityHandler to AgentApplication. This is ONLY for DotNet projects that uses `Microsoft.Agents.*` packages.  Triggered by Agents SDK bots that subclass ActivityHandler or TeamsActivityHandler that need to be modernized to AgentApplication routing.
---

# Agents SDK ActivityHandler → AgentApplication Migration (.NET)

## Overview

Upgrades a bot from the ActivityHandler compat layer to the modern `AgentApplication` routing pattern. This is ONLY for DotNet projects that already use `Microsoft.Agents.*` packages.  If the project is still using `Microsoft.Bot.*` packages the `bf-to-agents-sdk-dotnet-migration` skill should be used first.

**Two entry points:**
- **Standalone:** The bot already uses `Microsoft.Agents.*` packages, namespaces, and appsettings — focus is on the bot class, Program.cs, and endpoint mapping.
- **Second step after `bf-to-agents-sdk-dotnet-migration`:** Packages, namespaces, and appsettings were converted in that step — this skill handles the AgentApplication conversion.

---

## What Does NOT Need to Change

Verify each of these is already correct — do not modify unless broken:

| Item | Expected State |
|------|---------------|
| Namespaces in bot class | Already `Microsoft.Agents.*` — only remove compat namespaces |
| `appsettings.json` | Already has `Connections` + `TokenValidation` sections |
| `builder.Services.AddAgentAspNetAuthentication(...)` | Already present — keep |
| `AspNetExtensions.cs` (if present) | Sample-provided auth helper — keep as-is |
| Custom DI in Program.cs | Preserve dialogs, adapters, custom services — **but remove `ConversationState` and `UserState`** (see Step 2) |

> **⚠️ If beta packages are required:** Tell the user explicitly: *"This migration uses pre-release (`-beta`) Agents SDK packages, which are nightly builds and not suitable for production. Upgrade to a stable release before deploying."*

---

## Handler Mapping: ActivityHandler → AgentApplication

| ActivityHandler Override | AgentApplication Registration |
|--------------------------|-------------------------------|
| `OnMessageActivityAsync(ITurnContext<IMessageActivity>, CT)` | `OnActivity(ActivityTypes.Message, handler, rank: RouteRank.Last)` |
| `OnMembersAddedAsync(IList<ChannelAccount>, ITurnContext<IConversationUpdateActivity>, CT)` | `OnConversationUpdate(ConversationUpdateEvents.MembersAdded, handler)` |
| `OnMembersRemovedAsync(...)` | `OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, handler)` |
| `OnMessageUpdateActivityAsync(...)` | `OnActivity(ActivityTypes.MessageUpdate, handler)` |
| `OnMessageDeleteActivityAsync(...)` | `OnActivity(ActivityTypes.MessageDelete, handler)` |
| `OnReactionsAddedAsync(IList<MessageReaction>, ...)` | `OnMessageReactionsAdded(handler)` |
| `OnReactionsRemovedAsync(IList<MessageReaction>, ...)` | `OnMessageReactionsRemoved(handler)` |
| `OnEventAsync(ITurnContext<IEventActivity>, CT)` | `OnEvent(eventName, handler)` or `OnActivity(ActivityTypes.Event, handler)` |
| `OnInvokeActivityAsync(...)` with `adaptiveCard/action` + verb switch | `AdaptiveCards.OnActionExecute(verb, handler)` — one registration per verb (preferred) |
| `OnInvokeActivityAsync(...)` with `application/search` | `AdaptiveCards.OnSearch(dataset, handler)` — one registration per `choices.data.dataset` value |
| `OnInvokeActivityAsync(...)` (other invoke types) | `OnActivity(ActivityTypes.Invoke, handler)` |
| `OnTurnAsync(...)` override (cross-cutting) | `OnBeforeTurn(handler)` / `OnAfterTurn(handler)` |
| `OnInstallationUpdateActivityAsync(...)` | `OnActivity(ActivityTypes.InstallationUpdate, handler)` |
| `OnEndOfConversationActivityAsync(...)` | `OnActivity(ActivityTypes.EndOfConversation, handler)` |

**Key difference:** Handler signature changes from `(ITurnContext<T>, CT)` to `(ITurnContext, ITurnState, CT)`.
`membersAdded`, `membersRemoved`, `messageReactions` are no longer separate parameters — access via `turnContext.Activity.*`.

### `adaptiveCard/action` invokes → `AdaptiveCards.OnActionExecute`

If the bot overrides `OnInvokeActivityAsync` and switches on a verb from `adaptiveCard/action` activities, replace it with per-verb `AdaptiveCards.OnActionExecute` registrations. `AdaptiveCards` is a property on `AgentApplication` (no `RegisterExtension` needed):

```csharp
// Before: one OnInvokeActivityAsync with switch(verb)
protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> ctx, CancellationToken ct)
{
    if (ctx.Activity.Name == "adaptiveCard/action")
    {
        var wrapper = JsonSerializer.Deserialize<AdaptiveCardInvokeValue>(ctx.Activity.Value.ToString());
        switch (wrapper.Action.Verb)
        {
            case "approve": return CreateInvokeResponse(GetApproveCard(wrapper.Action.Data));
            case "reject":  return CreateInvokeResponse(GetRejectCard(wrapper.Action.Data));
        }
    }
    return null;
}

// After: one registration per verb — framework filters and extracts action.data
public MyBot(AgentApplicationOptions options) : base(options)
{
    AdaptiveCards.OnActionExecute("approve", OnApproveAsync);
    AdaptiveCards.OnActionExecute("reject",  OnRejectedAsync);
}

// Handler receives action.data directly — NOT the full AdaptiveCardInvokeValue
// Return AdaptiveCardInvokeResponse directly — no CreateInvokeResponse() wrapper
private Task<AdaptiveCardInvokeResponse> OnApproveAsync(
    ITurnContext turnContext, ITurnState turnState, object data, CancellationToken ct)
{
    var actionData = ProtocolJsonSerializer.ToObject<MyDataModel>(data);  // data == action.data
    return Task.FromResult(new AdaptiveCardInvokeResponse
    {
        StatusCode = 200,
        Type = "application/vnd.microsoft.card.adaptive",
        Value = BuildCard(actionData)
    });
}
```

**Key points:**
- `data` parameter = `invokeValue.Action.Data` — deserialize as your data model, not as `AdaptiveCardInvokeValue`
- Return `AdaptiveCardInvokeResponse` directly — the framework wraps it in an `InvokeResponse` automatically
- `CreateInvokeResponse()` is a compat-layer helper that does not exist on `AgentApplication`
- Namespace: `using Microsoft.Agents.Builder.App.AdaptiveCards;`

---

## Step 1-a: Convert Bot Class to AgentApplication (not using Dialogs)

- Remove `using Microsoft.Agents.Builder.Compat;` (ActivityHandler namespace)
- Remove `using Microsoft.Agents.Extensions.Teams.Compat;` (TeamsActivityHandler namespace)
- Keep all other `using Microsoft.Agents.*` statements — they are already correct

```csharp
// Before: ActivityHandler compat layer
using Microsoft.Agents.Builder.Compat;         // REMOVE
using Microsoft.Agents.Extensions.Teams.Compat; // REMOVE if Teams
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

public class EchoBot : ActivityHandler  // or TeamsActivityHandler
{
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken ct)
    { /* business logic */
    }
}

// After: AgentApplication
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

public class EchoBot : AgentApplication
{
    public EchoBot(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnMembersAddedAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    // Signature: (ITurnContext, ITurnState, CancellationToken)
    private async Task OnMessageAsync(ITurnContext ctx, ITurnState state, CancellationToken ct)
    { /* same business logic */ }
}
```

For Teams bots (`TeamsActivityHandler`) with Teams-specific handlers (messaging extensions, task modules, etc.), see **Step 1-d** below.

## Step 1-b: Convert Bot Class to AgentApplication (using Dialogs)

- Remove `using Microsoft.Agents.Builder.Compat;` (ActivityHandler namespace)
- Remove `using Microsoft.Agents.Extensions.Teams.Compat;` (TeamsActivityHandler namespace)
- Keep all other `using Microsoft.Agents.*` statements — they are already correct

```csharp
// Before: ActivityHandler compat layer
using Microsoft.Agents.Builder.Compat;         // REMOVE
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

public class EchoBot : ActivityHandler
{
    protected override Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken ct)
        => dialog.RunAsync(turnContext, conversationState, ct);
}

// After: AgentApplication
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

public class EchoBot : AgentApplication
{
    public EchoBot(AgentApplicationOptions options, MainDialog dialog) : base(options)
    {
        _dialog = dialog;
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
        // signin/verifyState and signin/tokenExchange are both matched by "signin/.*"
        OnActivity(
            (ctx, ct) => Task.FromResult(ctx.Activity.IsType(ActivityTypes.Invoke) && ctx.Activity.Name != null && Regex.IsMatch(ctx.Activity.Name, "signin/.*")),
            OnSigninInvokeStateAsync);
    }

    // Signature: (ITurnContext, ITurnState, CancellationToken) — NOT the ActivityHandler signature
    private Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
        => _dialog.RunAsync(turnContext, turnState.Conversation, ct);

    private Task OnSigninInvokeStateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
        => _dialog.RunAsync(turnContext, turnState.Conversation, ct);
}
```

**Key differences from non-dialog migration:**
- Do **not** inject `ConversationState` into the bot — pass `turnState.Conversation` to `Dialog.RunAsync` instead
- Use `InvokeRouteBuilder` (not `OnActivity(ActivityTypes.Invoke, ...)`) to route signin invokes — the name pattern matches both `signin/verifyState` and `signin/tokenExchange`
- Remove `ConversationState` and `UserState` from DI entirely — see Step 2

> ⚠️ **Generic `DialogBot<T>` base class pattern:** Some projects use a two-level class hierarchy where a generic base class (e.g. `DialogBot<T> : ActivityHandler`) holds the dialog field, state injection, and `OnTurnAsync` state-saving override, and the concrete bot class (e.g. `MyBot : DialogBot<MainDialog>`) only adds routing. Collapse both classes into a single non-generic `AgentApplication` subclass — inject the concrete dialog type directly in the constructor. The generic type parameter is not needed. Remove `OnTurnAsync` entirely (AgentApplication manages state automatically).

---

## Step 1-c: Remove State Management Boilerplate

### Remove OnTurnBeginAsync / OnTurnEndAsync Overrides

`ActivityHandler` and `TeamsActivityHandler` bots sometimes manually load and save state in turn lifecycle overrides. Remove these entirely — `AgentApplication` handles state loading and saving automatically:

```csharp
// REMOVE these overrides completely — AgentApplication does this automatically
protected override async Task OnTurnBeginAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
{
    await ConversationState.LoadAsync(turnContext, false, cancellationToken);
    await UserState.LoadAsync(turnContext, false, cancellationToken);
}

protected override async Task OnTurnEndAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
{
    await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
    await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
}
```

Also remove any `ConversationState` or `UserState` properties/fields that were injected for this purpose.

### Migrate CloudAdapter OnTurnError to AgentApplication

If a custom `CloudAdapter` subclass injects `ConversationState` only to call `DeleteStateAsync` on error, move that logic to an `AgentApplication.OnTurnError` handler instead.

**Before — in the custom adapter:**
```csharp
public MyAdapter(
    IChannelServiceClientFactory channelServiceClientFactory,
    IActivityTaskQueue activityTaskQueue,
    ILogger<CloudAdapter> logger,
    ConversationState conversationState)   // REMOVE this parameter
    : base(channelServiceClientFactory, activityTaskQueue, logger: logger)
{
    OnTurnError = async (turnContext, exception) =>
    {
        if (conversationState != null)
        {
            await conversationState.DeleteStateAsync(turnContext);  // REMOVE
        }
    };
}
```

**After — remove ConversationState from the adapter:**
```csharp
public MyAdapter(
    IChannelServiceClientFactory channelServiceClientFactory,
    IActivityTaskQueue activityTaskQueue,
    ILogger<CloudAdapter> logger)
    : base(channelServiceClientFactory, activityTaskQueue, logger: logger)
{
    // OnTurnError can remain for logging; state deletion moves to AgentApplication
}
```

**Add to the AgentApplication bot constructor:**
```csharp
public MyBot(AgentApplicationOptions options, ...) : base(options)
{
    OnTurnError(OnAgentTurnErrorAsync);
    // ... other route registrations
}

private async Task OnAgentTurnErrorAsync(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken)
{
    try
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
    }
    catch(Exception ex)
    {
        Logger.LogError(ex, "Exception deleting conversation state after a turn error.");
    }
}
```

Customer-written classes that accept `ConversationState` or `UserState` as constructor parameters can remain unchanged for now. However, they will ultimately need to source their state from `ITurnState.Conversation` or `ITurnState.User` — flag this to the customer as a follow-up item.

### `application/search` invokes → `AdaptiveCards.OnSearch`

If the bot overrides `OnInvokeActivityAsync` and handles `activity.Name == "application/search"` (adaptive card typeahead/dynamic search), replace it with per-dataset `AdaptiveCards.OnSearch` registrations. The dataset name comes from `choices.data.dataset` in the card JSON. The handler returns `IList<AdaptiveCardsSearchResult>` — the framework automatically wraps results in the `application/vnd.microsoft.search.searchResponse` format and sends the invoke response.

```csharp
// Before: one OnInvokeActivityAsync checking activity.Name
protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken ct)
{
    if (turnContext.Activity.Name == "application/search")
    {
        var searchData = JsonSerializer.Deserialize<DynamicSearchCard>(turnContext.Activity.Value.ToString());
        // ... build results ...
        return new InvokeResponse { Status = 200, Body = searchResponseData };
    }
    return null;
}

// After: per-dataset OnSearch registrations
public ActivityBot(AgentApplicationOptions options) : base(options)
{
    AdaptiveCards.OnSearch("npmpackages", OnNpmSearchAsync);
    AdaptiveCards.OnSearch("cities", OnCitiesSearchAsync);
}

// Handler signature: (ITurnContext, ITurnState, Query<AdaptiveCardsSearchParams>, CT) → Task<IList<AdaptiveCardsSearchResult>>
private async Task<IList<AdaptiveCardsSearchResult>> OnNpmSearchAsync(
    ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken ct)
{
    // query.Parameters.QueryText = search text typed by user
    // query.Parameters.Dataset  = "npmpackages"
    // ... fetch results ...
    return results.Select(r => new AdaptiveCardsSearchResult(r.Title, r.Value)).ToList();
}
```

**Key differences:**
- Return `IList<AdaptiveCardsSearchResult>` directly — no `InvokeResponse` wrapper
- `AdaptiveCardsSearchResult(string title, string value)` — constructor takes title and value
- `query.Parameters.QueryText` replaces `searchData.queryText`
- `query.Parameters.Dataset` contains the dataset name (same as the registration key)

> ⚠️ **Dependent dropdown — associated inputs not in `query`:** When a card uses `"associatedInputs": "auto"`, the selected values of other inputs are sent in the `data` field of the raw invoke payload — NOT in `query.Parameters`. Access them directly: `ProtocolJsonSerializer.ToObject<MyModel>(turnContext.Activity.Value)` where `MyModel` maps the top-level `data` field.
>
> ```csharp
> private Task<IList<AdaptiveCardsSearchResult>> OnCitiesSearchAsync(
>     ITurnContext turnContext, ITurnState turnState, Query<AdaptiveCardsSearchParams> query, CancellationToken ct)
> {
>     // Associated input (country selector) arrives in activity.Value's "data" field
>     var card = ProtocolJsonSerializer.ToObject<DependantDropdownCard>(turnContext.Activity.Value);
>     string country = card?.data?.choiceSelect?.ToLower() ?? "";
>     // ... return cities for country ...
> }
> ```

---

## Step 1-d: Convert TeamsActivityHandler with Teams-Specific Handlers

For bots that override Teams-specific methods (messaging extensions, task modules, etc.), use `RegisterExtension(new TeamsAgentExtension(this), ...)` inside the constructor.

### TeamsActivityHandler → TeamsAgentExtension handler mapping

**Message Extensions** — handlers return `Task<MessagingExtensionResult>` (not `MessagingExtensionResponse`):

| TeamsActivityHandler Override | TeamsAgentExtension Registration | Handler Delegate |
|-------------------------------|----------------------------------|-----------------|
| `OnTeamsMessagingExtensionQueryAsync(ctx, MessagingExtensionQuery, CT)` | `teams.MessageExtensions.OnQuery(commandId, handler)` | `(ITurnContext, ITurnState, Query<IDictionary<string,object>>, CT) → Task<MessagingExtensionResult>` |
| `OnTeamsMessagingExtensionSelectItemAsync(ctx, JsonElement, CT)` | `teams.MessageExtensions.OnSelectItem(handler)` | `(ITurnContext, ITurnState, object item, CT) → Task<MessagingExtensionResult>` |
| `OnTeamsAppBasedLinkQueryAsync(ctx, AppBasedLinkQuery, CT)` | `teams.MessageExtensions.OnQueryLink(handler)` | `(ITurnContext, ITurnState, string url, CT) → Task<MessagingExtensionResult>` |
| `OnTeamsAnonymousAppBasedLinkQueryAsync(ctx, AppBasedLinkQuery, CT)` | `teams.MessageExtensions.OnAnonymousQueryLink(handler)` | `(ITurnContext, ITurnState, string url, CT) → Task<MessagingExtensionResult>` |
| `OnTeamsMessagingExtensionFetchTaskAsync(ctx, MessagingExtensionAction, CT)` | `teams.MessageExtensions.OnFetchTask(commandId, handler)` | `(ITurnContext, ITurnState, CT) → Task<TaskModuleResponse>` |
| `OnTeamsMessagingExtensionSubmitActionAsync(ctx, MessagingExtensionAction, CT)` | `teams.MessageExtensions.OnSubmitAction(commandId, handler)` | `(ITurnContext, ITurnState, object data, CT) → Task<MessagingExtensionActionResponse>` |
| `OnTeamsMessagingExtensionAgentMessagePreviewEditAsync(ctx, MessagingExtensionAction, CT)` | `teams.MessageExtensions.OnAgentMessagePreviewEdit(commandId, handler)` | `(ITurnContext, ITurnState, IActivity activityPreview, CT) → Task<MessagingExtensionActionResponse>` |
| `OnTeamsMessagingExtensionAgentMessagePreviewSendAsync(ctx, MessagingExtensionAction, CT)` | `teams.MessageExtensions.OnAgentMessagePreviewSend(commandId, handler)` | `(ITurnContext, ITurnState, IActivity activityPreview, CT) → Task` |
| `OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync(ctx, MessagingExtensionQuery, CT)` | `teams.MessageExtensions.OnQueryUrlSetting(handler)` | `(ITurnContext, ITurnState, CT) → Task<MessagingExtensionResult>` |
| `OnTeamsMessagingExtensionConfigurationSettingAsync(ctx, JsonElement, CT)` | `teams.MessageExtensions.OnConfigureSettings(handler)` | `(ITurnContext, ITurnState, object settings, CT) → Task` |
| `OnTeamsMessagingExtensionCardButtonClickedAsync(ctx, JsonElement, CT)` | `teams.MessageExtensions.OnCardButtonClicked(handler)` | `(ITurnContext, ITurnState, object cardData, CT) → Task` |

**Task Modules:**

| TeamsActivityHandler Override | TeamsAgentExtension Registration | Handler Delegate |
|-------------------------------|----------------------------------|-----------------|
| `OnTeamsTaskModuleFetchAsync(ctx, TaskModuleRequest, CT)` | `teams.TaskModules.OnFetch(verb, handler)` | `(ITurnContext, ITurnState, object data, CT) → Task<TaskModuleResponse>` |
| `OnTeamsTaskModuleSubmitAsync(ctx, TaskModuleRequest, CT)` | `teams.TaskModules.OnSubmit(verb, handler)` | `(ITurnContext, ITurnState, object data, CT) → Task<TaskModuleResponse>` |

**⚠️ Task Module verb matching:** `OnFetch` and `OnSubmit` match by looking for a `verb` field inside `activity.Value.data`. If the submitted data has **no `verb` field**, use the `RouteSelector` overload instead:

```csharp
teams.TaskModules.OnSubmit(
    (ctx, ct) => Task.FromResult(
        string.Equals(ctx.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ctx.Activity.Name, "task/submit", StringComparison.OrdinalIgnoreCase)),
    OnTaskModuleSubmitAsync);
```

**⚠️ `data` parameter in submit handler:** The `data` parameter passed to `SubmitHandlerAsync` is `taskModuleAction.Value` from a `CardAction` deserialization of the payload. For `task/submit` payloads where the top-level JSON has no `"value"` key (only `"data"` and `"context"`), this will be `null`. Access the submitted data via `turnContext.Activity.Value` directly:

```csharp
private async Task<TaskModuleResponse> OnTaskModuleSubmitAsync(
    ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken)
{
    // data parameter is null when payload has no top-level "value" key — use Activity.Value instead
    var request = ProtocolJsonSerializer.ToObject<TaskModuleRequest>(turnContext.Activity.Value);
    var feedback = ProtocolJsonSerializer.ToObject<MySubmitModel>(request.Data);
    // ...
    return null; // null dismisses the task module
}
```

**Meetings:**

| TeamsActivityHandler Override | TeamsAgentExtension Registration | Handler Delegate |
|-------------------------------|----------------------------------|-----------------|
| `OnTeamsMeetingStartAsync(MeetingStartEventDetails, ctx, CT)` | `teams.Meetings.OnStart(handler)` | `(ITurnContext, ITurnState, MeetingStartEventDetails, CT) → Task` |
| `OnTeamsMeetingEndAsync(MeetingEndEventDetails, ctx, CT)` | `teams.Meetings.OnEnd(handler)` | `(ITurnContext, ITurnState, MeetingEndEventDetails, CT) → Task` |
| `OnTeamsMeetingParticipantsJoinAsync(MeetingParticipantsEventDetails, ctx, CT)` | `teams.Meetings.OnParticipantsJoin(handler)` | `(ITurnContext, ITurnState, MeetingParticipantsEventDetails, CT) → Task` |
| `OnTeamsMeetingParticipantsLeaveAsync(MeetingParticipantsEventDetails, ctx, CT)` | `teams.Meetings.OnParticipantsLeave(handler)` | `(ITurnContext, ITurnState, MeetingParticipantsEventDetails, CT) → Task` |

**Messages (edit/delete):**

| TeamsActivityHandler Override | TeamsAgentExtension Registration |
|-------------------------------|----------------------------------|
| `OnTeamsMessageEditAsync(ctx, CT)` | `teams.OnMessageEdit(handler)` |
| `OnTeamsMessageUndeleteAsync(ctx, CT)` | `teams.OnMessageUndelete(handler)` |
| `OnTeamsMessageSoftDeleteAsync(ctx, CT)` | `teams.OnMessageDelete(handler)` |
| `OnTeamsReadReceiptAsync(ReadReceiptInfo, ctx, CT)` | `teams.OnTeamsReadReceipt(handler)` |

**File Consent:**

| TeamsActivityHandler Override | TeamsAgentExtension Registration |
|-------------------------------|----------------------------------|
| `OnTeamsFileConsentAcceptAsync(ctx, FileConsentCardResponse, CT)` | `teams.OnFileConsentAccept(handler)` |
| `OnTeamsFileConsentDeclineAsync(ctx, FileConsentCardResponse, CT)` | `teams.OnFileConsentDecline(handler)` |

**Bot Config:**

| TeamsActivityHandler Override | TeamsAgentExtension Registration |
|-------------------------------|----------------------------------|
| `OnTeamsConfigFetchAsync(ctx, JsonElement, CT)` | `teams.OnConfigFetch(handler)` |
| `OnTeamsConfigSubmitAsync(ctx, JsonElement, CT)` | `teams.OnConfigSubmit(handler)` |

**Channels, Teams, Members** — all route through `teams.OnConversationUpdate(eventName, handler)` with constants from `TeamsConversationUpdateEvents`:

| TeamsActivityHandler Override | Event Constant |
|-------------------------------|----------------|
| `OnTeamsMembersAddedAsync` | `ConversationUpdateEvents.MembersAdded` |
| `OnTeamsMembersRemovedAsync` | `ConversationUpdateEvents.MembersRemoved` |
| `OnTeamsChannelCreatedAsync` | `TeamsConversationUpdateEvents.ChannelCreated` |
| `OnTeamsChannelDeletedAsync` | `TeamsConversationUpdateEvents.ChannelDeleted` |
| `OnTeamsChannelRenamedAsync` | `TeamsConversationUpdateEvents.ChannelRenamed` |
| `OnTeamsChannelRestoredAsync` | `TeamsConversationUpdateEvents.ChannelRestored` |
| `OnTeamsTeamArchivedAsync` | `TeamsConversationUpdateEvents.TeamArchived` |
| `OnTeamsTeamUnarchivedAsync` | `TeamsConversationUpdateEvents.TeamUnarchived` |
| `OnTeamsTeamDeletedAsync` | `TeamsConversationUpdateEvents.TeamDeleted` |
| `OnTeamsTeamHardDeletedAsync` | `TeamsConversationUpdateEvents.TeamHardDeleted` |
| `OnTeamsTeamRenamedAsync` | `TeamsConversationUpdateEvents.TeamRenamed` |
| `OnTeamsTeamRestoredAsync` | `TeamsConversationUpdateEvents.TeamRestored` |

**Other:**

| TeamsActivityHandler Override | TeamsAgentExtension Registration |
|-------------------------------|----------------------------------|
| `OnTeamsO365ConnectorCardActionAsync(ctx, O365ConnectorCardActionQuery, CT)` | `teams.OnO365ConnectorCardAction(handler)` |
| `OnTeamsSigninVerifyStateAsync(ctx, CT)` | On `AgentApplication` directly: `OnActivity((ctx, ct) => Task.FromResult(ctx.Activity.IsType(ActivityTypes.Invoke) && Regex.IsMatch(ctx.Activity.Name, "signin/.*")), handler)` |
| `OnTeamsTabFetchAsync(ctx, TabRequest, CT)` | No direct equivalent — use `OnActivity(ActivityTypes.Invoke, handler)` with a name filter |
| `OnTeamsTabSubmitAsync(ctx, TabSubmit, CT)` | No direct equivalent — use `OnActivity(ActivityTypes.Invoke, handler)` with a name filter |

### Handler signature changes

**Query handler** — return type changes from `Task<MessagingExtensionResponse>` to `Task<MessagingExtensionResult>`:
```csharp
// Before
protected override async Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(
    ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken ct)
{
    var text = query?.Parameters?[0]?.Value?.ToString();  // index-based parameter access
    // ...
    return new MessagingExtensionResponse { ComposeExtension = new MessagingExtensionResult { ... } };
}

// After — registered as: teams.MessageExtensions.OnQuery("commandId", OnQueryAsync)
private async Task<MessagingExtensionResult> OnQueryAsync(
    ITurnContext turnContext, ITurnState turnState,
    Query<IDictionary<string, object>> query, CancellationToken ct)
{
    // Named parameter access — value is a JsonElement
    string text = string.Empty;
    if (query.Parameters.TryGetValue("paramName", out var val) && val is JsonElement el)
        text = el.GetString() ?? string.Empty;
    // ...
    return new MessagingExtensionResult { Type = "result", AttachmentLayout = "list", Attachments = attachments };
    // NOTE: return MessagingExtensionResult directly — no MessagingExtensionResponse wrapper
}
```

**SelectItem handler** — `item` is now `object` (cast to `JsonElement`):
```csharp
// Before
protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionSelectItemAsync(
    ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken ct)
{
    string id = query.GetProperty("packageId").GetString();
    return Task.FromResult(new MessagingExtensionResponse { ComposeExtension = new MessagingExtensionResult { ... } });
}

// After — registered as: teams.MessageExtensions.OnSelectItem(OnSelectItemAsync)
private Task<MessagingExtensionResult> OnSelectItemAsync(
    ITurnContext turnContext, ITurnState turnState, object item, CancellationToken ct)
{
    JsonElement query = (JsonElement)item;
    string id = query.GetProperty("packageId").GetString();
    return Task.FromResult(new MessagingExtensionResult { Type = "result", ... });
    // NOTE: return MessagingExtensionResult directly — no MessagingExtensionResponse wrapper
}
```

### Full registration pattern

```csharp
using Microsoft.Agents.Builder.App;             // AgentApplication
using Microsoft.Agents.Builder.App.AdaptiveCards; // Query<T>
using Microsoft.Agents.Builder.State;            // ITurnState
using Microsoft.Agents.Extensions.Teams.App;    // TeamsAgentExtension
using Microsoft.Agents.Extensions.Teams.Models; // MessagingExtensionResult, etc.
// REMOVE: using Microsoft.Agents.Extensions.Teams.Compat;

public class MyTeamsBot : AgentApplication
{
    public MyTeamsBot(AgentApplicationOptions options) : base(options)
    {
        RegisterExtension(new TeamsAgentExtension(this), teams =>
        {
            teams.MessageExtensions.OnQuery("searchCommandId", OnQueryAsync);
            teams.MessageExtensions.OnSelectItem(OnSelectItemAsync);
        });
    }
}
```

**Key differences:**
- Helper methods that returned `MessagingExtensionResponse` must be changed to return `MessagingExtensionResult` — drop the outer `new MessagingExtensionResponse { ComposeExtension = ... }` wrapper
- `MessagingExtensionQuery.Parameters[index].Value` → `Query<IDictionary<string,object>>.Parameters.TryGetValue("name", out var val)` where `val` is a `JsonElement`
- `commandId` in `OnQuery(commandId, ...)` must match the command `id` in the Teams app manifest

---

## Step 2: Update Program.cs

### Add `AddAgentApplicationOptions`

Add this call **before** `builder.AddAgent<T>()` — it is required for `AgentApplication` and was absent in ActivityHandler-based bots:

```csharp
builder.AddAgentApplicationOptions();  // ADD THIS — required for AgentApplication
builder.AddAgent<EchoBot>();           // existing line — keep
```

### Ensure IStorage Is Registered

`AgentApplication` always requires an `IStorage` registration — even if the bot has no dialogs or explicit state usage. If it is not already present, add it:

```csharp
builder.Services.AddSingleton<IStorage, MemoryStorage>(); // always required by AgentApplication
```

### Remove ConversationState and UserState from DI

`AgentApplication` manages state internally via `ITurnState`. Remove these registrations entirely:

```csharp
// REMOVE both of these — AgentApplication does not use them
// builder.Services.AddSingleton<ConversationState>();
// builder.Services.AddSingleton<UserState>();

builder.Services.AddSingleton<IStorage, MemoryStorage>(); // keep — required by AgentApplication
builder.Services.AddSingleton<MainDialog>();               // keep — dialog class itself is still needed
builder.Services.AddSingleton<IMyService, MyService>();    // keep all custom services
```

### Preserve All Other Custom DI

Keep dialogs, storage, adapters, and custom services. Only `ConversationState` and `UserState` are removed.

### TeamsSSOTokenExchangeMiddleware

If the ActivityHandler bot registered `TeamsSSOTokenExchangeMiddleware`, **remove it** — it is not required and should not be used with `AgentApplication`. Remove it from DI and from any `IMiddleware[]` array registration.

---

## Step 3: Update Endpoint Mapping

Identify which endpoint pattern the existing bot uses, then apply the corresponding change.

### Pattern A: `app.MapControllers()` + `BotController.cs` (most common in Agents SDK compat samples)

```csharp
// Before Program.cs
builder.Services.AddControllers();           // needed for MapControllers
// ...
app.MapControllers().AllowAnonymous();       // dev
app.MapControllers();                        // prod

// After Program.cs
// Remove AddControllers() ONLY IF no other MVC controllers exist in the project
// Add authentication middleware if not already present:
app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();                                              // optional GET "/"
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

Delete `BotController.cs`. Check first that it contains no custom logic beyond calling `adapter.ProcessAsync` — if it does, preserve that logic.

**If the project uses Razor Pages** (`AddRazorPages()` / `MapRazorPages()`): keep both — they serve web UI pages unrelated to the bot endpoint. Remove `AddControllers()`, `AddMvc()`, and any `MapControllerRoute()` / `MapControllers()` calls, but leave the Razor Pages registrations in place:

```csharp
// Keep — serves Razor Pages for web UI
builder.Services.AddRazorPages();
// ...
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();                   // keep
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

Also remove the dev branch `app.MapControllers().AllowAnonymous()` pattern — auth is now handled by `requireAuth` on `MapAgentApplicationEndpoints`.

### Pattern B: `app.MapAgentEndpoints()`

```csharp
// Before
app.MapAgentEndpoints(requireAuth: !app.Environment.IsDevelopment());

// After
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

### Pattern C: `app.MapPost(...)` (older minimal API)

Offer to switch. If accepted:

```csharp
// Remove:
app.MapPost("/api/messages", async (HttpRequest req, HttpResponse res, IAgentHttpAdapter adapter, IAgent bot, CancellationToken ct)
    => await adapter.ProcessAsync(req, res, bot, ct)).AllowAnonymous();

// Add:
app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

### Step 4 or at the very end of the migration:  Ask user if they would like to write the learnings from this migration to a markdown file they could submit to use to help us improve the skill.

---

## Development URL

If the existing bot had `app.Urls.Add("http://localhost:3978")` inside an `IsDevelopment()` check, keep it. If it was missing, add it:

```csharp
if (app.Environment.IsDevelopment())
{
    app.Urls.Add("http://localhost:3978");
}
```

---

## Middleware Replacements

Some Bot Framework middleware has a direct equivalent in AgentApplication configuration.

### ShowTypingMiddleware → StartTypingTimer

Drop the `ShowTypingMiddleware` registration and enable the built-in typing timer instead:

```csharp
// Remove from adapter setup:
// adapter.Use(new ShowTypingMiddleware());
```

```json
// appsettings.json — add to AgentApplication section:
"AgentApplication": {
  "StartTypingTimer": true
}
```

### AutoSaveStateMiddleware

`AgentApplication` loads and saves state automatically on every turn. **Always remove `AutoSaveStateMiddleware`** — it is no longer needed and must not be used with `AgentApplication`.

```csharp
// REMOVE — always:
// new AutoSaveStateMiddleware(...)
// adapter.Use(new AutoSaveStateMiddleware(...));
```

If removing it empties the `IMiddleware[]` DI registration, remove that registration entirely as well.

### NormalizeMentionsMiddleware → NormalizeMentions / RemoveRecipientMention

Drop the `NormalizeMentionsMiddleware` registration and enable the equivalent settings instead:

```csharp
// Remove from adapter setup:
// adapter.Use(new NormalizeMentionsMiddleware());
```

```json
// appsettings.json — add to AgentApplication section:
"AgentApplication": {
  "NormalizeMentions": true,
  "RemoveRecipientMention": true
}
```

---

## Files to Delete

| File | When to delete |
|------|----------------|
| `BotController.cs` | Always when switching to `MapAgentApplicationEndpoints` — unless it has custom logic beyond `ProcessAsync` |
| `Controllers/` folder | After deleting `BotController.cs`, if no other controllers remain |

Do **not** delete `AspNetExtensions.cs` — if present it is providing the `AddAgentAspNetAuthentication` extension and must be kept.

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Forgot `builder.AddAgentApplicationOptions()` | Add before `builder.AddAgent<T>()` |
| Removed custom DI (dialogs, state, services) | Restore — never remove developer-added registrations |
| Added `[Agent]` attribute to AgentApplication class | Not required — `MapAgentApplicationEndpoints` discovers `AgentApplication` subclasses without it |
| Left `using Microsoft.Agents.Builder.Compat` | Remove — ActivityHandler is gone |
| Left `using Microsoft.Agents.Extensions.Teams.Compat` on main class | Remove — TeamsActivityHandler is gone; use `RegisterExtension(new TeamsAgentExtension(this), ...)` instead |
| Teams MessageExtension handler returns `MessagingExtensionResponse` | Change return type to `MessagingExtensionResult` — the framework no longer uses the `ComposeExtension` wrapper; return the inner result directly |
| Teams query parameter accessed by index (`query.Parameters[0].Value`) | Use named access: `query.Parameters.TryGetValue("paramName", out var val)` where `val` is a `JsonElement`; parameter name comes from the Teams app manifest command definition |
| Teams SelectItem handler uses `JsonElement` parameter | In `OnSelectItem`, the parameter type is `object`; cast it: `JsonElement query = (JsonElement)item` |
| Missing `Microsoft.Agents.Extensions.Teams.App` namespace for TeamsAgentExtension | Add `using Microsoft.Agents.Extensions.Teams.App;` — `TeamsAgentExtension` is not in the Compat namespace |
| Missing `Microsoft.Agents.Builder.App.AdaptiveCards` namespace for `Query<T>` | Add `using Microsoft.Agents.Builder.App.AdaptiveCards;` — required for `Query<IDictionary<string, object>>` handler parameter |
| `AdaptiveCard` is ambiguous after adding `using Microsoft.Agents.Builder.App.AdaptiveCards` | Conflict with `AdaptiveCards.AdaptiveCard` from the AdaptiveCards NuGet package. Use an alias instead: `using AgentAdaptiveCards = Microsoft.Agents.Builder.App.AdaptiveCards;` then reference `AgentAdaptiveCards.Query<IDictionary<string, object>>` in the handler signature |
| Kept `builder.Services.AddControllers()` but deleted all controllers | Remove — no longer needed without MVC controllers |
| Missing `app.UseAuthentication()` / `app.UseAuthorization()` after switching from MapControllers | Add them before `MapAgentApplicationEndpoints` |
| Injected `ConversationState` into bot constructor for dialogs | Remove from bot — use `turnState.Conversation` in `Dialog.RunAsync` calls instead |
| Left `ConversationState` registered in DI | Remove — AgentApplication manages state internally via `ITurnState` |
| Left `UserState` registered in DI | Remove — AgentApplication does not use it |
| Left `AutoSaveStateMiddleware` registered | Remove entirely — AgentApplication saves state automatically; never keep it |
| Left `ConversationState` injected in custom adapter `OnTurnError` for `DeleteStateAsync` | Remove the parameter; move `DeleteStateAsync` into an `AgentApplication.OnTurnError` handler using `turnState.Conversation.DeleteStateAsync` |
| Left `OnTurnBeginAsync`/`OnTurnEndAsync` overrides that load/save state | Remove entirely — AgentApplication handles this automatically |
| Kept generic `DialogBot<T>` base class as a separate file | Collapse both classes into a single non-generic `AgentApplication` subclass; inject the concrete dialog type directly; delete the generic base class file |
| Left `TeamsSSOTokenExchangeMiddleware` in DI or `IMiddleware[]` | Remove — not required and should not be used with AgentApplication |
| Manually calling `SaveChangesAsync` in AgentApplication handlers | Remove — state is saved automatically |
| Left `ShowTypingMiddleware` registration | Drop it; set `"AgentApplication": { "StartTypingTimer": true }` in appsettings.json |
| Left `NormalizeMentionsMiddleware` registration | Drop it; set `NormalizeMentions` and/or `RemoveRecipientMention` to `true` in `AgentApplication` appsettings |
| Handler accesses `membersAdded` as a parameter | Use `turnContext.Activity.MembersAdded` instead — it is no longer passed as a parameter |
| Used `ProjectReference` to local SDK source instead of NuGet packages | Always use NuGet packages — customers don't have access to the SDK source repo |
| Custom adapter constructor uses `ILogger<IAgentHttpAdapter>` | Change to `ILogger<CloudAdapter>` — `CloudAdapter` base constructor requires the concrete type, not the interface |
| Changed business logic during migration | Do not — only change structure/registration/routing |
| Missing `IStorage` registration | Add `builder.Services.AddSingleton<IStorage, MemoryStorage>();` — always required by AgentApplication, not just for dialog bots |
| Used `OnActivity(ActivityTypes.Invoke, ...)` for `adaptiveCard/action` verbs | Use `AdaptiveCards.OnActionExecute(verb, handler)` instead — it's built into `AgentApplication`, matches by verb, and handles the invoke response automatically |
| Called `CreateInvokeResponse(...)` in an `OnActionExecute` handler | `CreateInvokeResponse` is a compat-layer helper — it doesn't exist on `AgentApplication`. Return `AdaptiveCardInvokeResponse` directly; the framework wraps it |
| Deserialized `data` as `AdaptiveCardInvokeValue` in `OnActionExecute` handler | The `data` parameter is already `invokeValue.Action.Data` — deserialize directly as your data model, not as the full invoke value wrapper |
| `teams.TaskModules.OnSubmit(verb, ...)` never triggers | Default filter matches `activity.Value.data.verb` — if the submitted data has no `verb` field, use the `RouteSelector` overload with a custom selector that checks `ctx.Activity.Name == "task/submit"` |
| `data` parameter is null in `SubmitHandlerAsync` | The `data` parameter is `taskModuleAction.Value` (CardAction), which is null when the payload has no top-level `"value"` key. Parse the submitted data directly: `ProtocolJsonSerializer.ToObject<TaskModuleRequest>(turnContext.Activity.Value).Data` |
| Removed `AddRazorPages()` / `MapRazorPages()` when deleting BotController | Keep them — they serve web UI pages unrelated to the bot; only remove `AddControllers()`, `AddMvc()`, and `MapControllers()`/`MapControllerRoute()` |
| Used `OnActivity(ActivityTypes.Invoke, ...)` for `application/search` | Use `AdaptiveCards.OnSearch(dataset, handler)` instead — routes by `choices.data.dataset` value, handles invoke response automatically, handler returns `IList<AdaptiveCardsSearchResult>` |
| `OnSearch` handler returns `InvokeResponse` | Return `IList<AdaptiveCardsSearchResult>` — the framework wraps results in `application/vnd.microsoft.search.searchResponse` and sends the invoke response |
| Looked for associated input values (dependent dropdown) in `query.Parameters` | Associated inputs from `"associatedInputs": "auto"` arrive in the `data` field of the raw invoke payload, not in `query.Parameters`. Read them via `ProtocolJsonSerializer.ToObject<MyModel>(turnContext.Activity.Value)` |

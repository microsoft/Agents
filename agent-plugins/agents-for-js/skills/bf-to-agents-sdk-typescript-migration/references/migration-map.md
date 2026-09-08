# BotBuilderJS to Agents SDK migration map

Use the row matching the source code as a candidate, then verify it in the exact stable target-package declarations. **Direct** means a package/API replacement, **Rewrite** means a supported `AgentApplication` change, **Decision** requires the user to select a verified supported design, and **Unsupported** means no stable Agents SDK for JavaScript equivalent exists.

Sections: [Packages](#packages) · [Application](#application-and-handlers) · [Activity](#activity-and-turncontext) · [State](#state-dialogs-and-storage) · [Hosting](#hosting-authentication-middleware-and-errors) · [Proactive](#proactive-messaging) · [Agent-to-agent](#agent-to-agent-and-skills) · [Teams](#teams) · [Unsupported](#unsupported-and-design-decisions)

## Packages

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Direct | `botbuilder`, `botbuilder-core` | `@microsoft/agents-hosting` |
| Direct | `botframework-schema` | `@microsoft/agents-activity` |
| Direct | `botbuilder-dialogs` | `@microsoft/agents-hosting-dialogs` |
| Direct | Cosmos DB from `botbuilder-azure` | `@microsoft/agents-hosting-storage-cosmos` |
| Direct | Blob storage from `botbuilder-azure` or `botbuilder-azure-blobs` | `@microsoft/agents-hosting-storage-blob` |
| Direct | Teams APIs from `botbuilder` | `@microsoft/agents-hosting-extensions-teams` |
| Rewrite | named pipes from `botframework-streaming` or `CloudAdapter.connectNamedPipe(...)` | `@microsoft/agents-hosting-directline-namedpipes` |
| Direct | Manual HTTP hosting | `@microsoft/agents-hosting-express` plus `express` |

Install only the target packages required by detected source usage.

## Application and handlers

```ts
// Bot Framework
export class EchoBot extends ActivityHandler {
  constructor () {
    super()
    this.onMessage(async (context, next) => {
      await context.sendActivity(`You said: ${context.activity.text}`)
      await next()
    })
  }
}

// Agents SDK
export class EchoBot extends AgentApplication<TurnState> {
  constructor () {
    super()
    this.onActivity('message', async (context, state) => {
      await context.sendActivity(`You said: ${context.activity.text}`)
    }, [], RouteRank.Last)
  }
}
```

Import `AgentApplication`, `TurnState`, and `RouteRank` from `@microsoft/agents-hosting`. Keep the existing export name. Use `onMessage(keywordOrSelector, handler)` for commands; it does not accept only a handler.

| Status | Bot Framework | `AgentApplication` target |
|---|---|---|
| Rewrite | `ActivityHandler` | `AgentApplication<TurnState>` |
| Rewrite | `(context, next)` | `(context, state)`; remove `next()` after preserving chained work |
| Rewrite | `onMessage(handler)` | catch-all `onActivity('message', handler, [], RouteRank.Last)` |
| Rewrite | command checks inside `onMessage` | `onMessage(string | RegExp | selector, handler)` |
| Rewrite | `onMembersAdded` / `onMembersRemoved` | `onConversationUpdate('membersAdded' | 'membersRemoved', handler)` |
| Rewrite | `onReactionsAdded` / `onReactionsRemoved` | `onMessageReactionAdded` / `onMessageReactionRemoved` |
| Rewrite | `onEvent`, `onTyping`, `onInstallationUpdate`, `onEndOfConversation` | `onActivity(typeOrSelector, handler)` |
| Rewrite | `onTokenResponseEvent` | authorization handler or exact event selector |
| Rewrite | `onInvokeActivity` | specialized invoke API or `addRoute(selector, handler, true, ...)` |
| Rewrite | `onAdaptiveCardInvoke` | `app.adaptiveCards.actionExecute(verb, handler)` |
| Rewrite | Adaptive Card `Action.Submit` | `app.adaptiveCards.actionSubmit(verb, handler)` |
| Rewrite | `application/search` invoke | `app.adaptiveCards.search(dataset, handler)` |
| Direct | `run(context)` | `run(context)` |

Routes are first-match. Register commands and specific invokes before catch-all activity routes. Combine handlers that previously relied on `await next()`, or move cross-cutting work to middleware or `onTurn(...)`.

## Activity and TurnContext

`Activity` is a class. Use `Activity.fromJson(jsonString)` for untrusted JSON strings and `Activity.fromObject(value)` for object literals when an `Activity` instance is required.

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Direct | `context.sendActivity(...)` | same method; convert literal objects when required |
| Direct | `sendActivities`, `updateActivity`, `deleteActivity` | same methods; use `Activity` instances where typed |
| Direct | `onSendActivities`, `onUpdateActivity`, `onDeleteActivity` | same methods |
| Direct | `context.adapter`, `responded`, `locale`, `turnState` | same properties |
| Direct | `TurnContext.applyConversationReference(activity, ref, incoming?)` | `activity.applyConversationReference(ref, incoming?)` |
| Direct | `TurnContext.getConversationReference(activity)` | `activity.getConversationReference()` |
| Direct | `TurnContext.getReplyConversationReference(activity, reply)` | `activity.getReplyConversationReference(reply.id)` |
| Direct | `TurnContext.removeRecipientMention(activity)` | `activity.removeRecipientMention()` |
| Direct | `TurnContext.getMentions(activity)` | `activity.getMentions(activity)` |
| Direct | `TurnContext.removeMentionText(activity, id)` | `activity.removeMentionText(id)` |

## State, dialogs, and storage

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Rewrite | `ConversationState` / `UserState` used by application logic | typed `TurnState`; use `state.conversation`, `state.user`, or `getValue` / `setValue` |
| Direct | `ConversationState` / `UserState` retained as a bridge | same class names from `@microsoft/agents-hosting` |
| Direct | `BotState` | `AgentState` |
| Direct | `StatePropertyAccessor<T>` | `AgentStatePropertyAccessor<T>` |
| Rewrite | `AutoSaveStateMiddleware` | remove only for state owned and saved by `AgentApplication` |
| Decision | existing persisted Bot Framework keys | choose compatibility state, dual-read, re-key, custom key, or offline migration |
| Decision | `PrivateConversationState` | custom `TurnState` scope/key strategy; no built-in JS class |
| Direct | `DialogSet`, `DialogManager`, `WaterfallDialog`, `ComponentDialog` | same class names from `@microsoft/agents-hosting-dialogs` |
| Direct | standard activity, attachment, choice, confirm, date/time, number, and text prompts | same prompt classes from `@microsoft/agents-hosting-dialogs` |
| Direct | `Dialog.run(...)` helper pattern | `runDialog(dialog, context, accessor)` |
| Decision | `OAuthPrompt` | redesign with app authorization and sign-in handlers |
| Direct | `MemoryStorage`, `FileStorage` | same classes from `@microsoft/agents-hosting` |
| Direct | `CosmosDbPartitionedStorage` | same class from `@microsoft/agents-hosting-storage-cosmos` |
| Direct | Blob state/transcripts | `BlobsStorage` / `BlobsTranscriptStore` from `@microsoft/agents-hosting-storage-blob` |

Preserve state property names, scope, storage keys, namespaces, ETags, and delete behavior. Do not assume app-owned `TurnState` reads existing Bot Framework records.

## Hosting, authentication, middleware, and errors

```ts
import { startServer } from '@microsoft/agents-hosting-express'
startServer(new EchoBot())
```

Use `startServer()` when it preserves the source HTTP contract. Keep an owned Express server with `CloudAdapter.process(...)` when the bot has custom routes, middleware, limits, or response behavior.

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Direct | `BotFrameworkAdapter` | `CloudAdapter` |
| Direct | `adapter.processActivity(req, res, logic)` | `adapter.process(req, res, logic)` |
| Rewrite | `ConfigurationBotFrameworkAuthentication` | call `loadAuthConfigFromEnv()` and pass its `AuthConfiguration` to `startServer` or `CloudAdapter` |
| Rewrite | `MicrosoftAppId`, `MicrosoftAppPassword`, `MicrosoftAppTenantId` | `connections__serviceConnection__settings__clientId`, `connections__serviceConnection__settings__clientSecret`, `connections__serviceConnection__settings__tenantId` |
| Direct | `adapter.use(middleware)` | same API; preserve order and `next()` semantics |
| Rewrite | `ShowTypingMiddleware` | `new AgentApplication({ startTypingTimer: true, typing: ... })` |
| Direct | `TranscriptLoggerMiddleware` | same class or app `transcriptLogger` option |
| Rewrite | `adapter.onTurnError` | `app.onError(async (context, error) => ...)` |
| Rewrite | custom Express/Restify host | `startServer(agent, options)` or owned Express plus `CloudAdapter.process` |

Apply `authorizeJWT(authConfig)` to production activity endpoints. Preserve non-message routes and middleware order. Translate managed identity, workload identity, federation, or certificates to the matching auth type; do not replace them with a client secret.

## Proactive messaging

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Direct | `adapter.continueConversation(...)` | `adapter.continueConversation(identityOrAppId, reference, logic)` |
| Direct | `adapter.createConversationAsync(...)` | same method with current `agentAppId`, channel, service URL, audience, and parameters |
| Rewrite | stored conversation-reference map | `app.proactive.storeConversation(...)` with durable proactive storage |
| Rewrite | manual proactive send/turn | `app.proactive.sendActivity(...)` / `continueConversation(...)` |
| Rewrite | manual conversation creation | `app.proactive.createConversation(...)` |
| Direct | connector/token clients from adapter helpers | `context.turnState.get(context.adapter.ConnectorClientKey)` / `get(context.adapter.UserTokenClientKey)` |

Preserve app identity, audience, tenant, service URL, and channel data; validate proactive sends in the target channel.

## Agent-to-agent and skills

| Status | Bot Framework | Agents SDK |
|---|---|---|
| Rewrite | `BotFrameworkHttpClient.postActivity(...)` | `new AgentClient(name).postActivity(activity, authConfig, conversationState, context)` |
| Rewrite | `SkillDialog` | `AgentClient.postActivity(...)`; move the skill conversation lifecycle to `ConversationState` and the response controller |
| Rewrite | `SkillHandler` callback endpoint | `configureResponseController(app, adapter, agent, conversationState)` |
| Rewrite | `SkillConversationIdFactory` correlation storage | `ConversationState` used by `AgentClient` and the response controller |
| Rewrite | `CloudAdapter.connectNamedPipe(...)`, `NamedPipeServer` | `createLocalAdapter()` plus `startNamedPipeServer(adapter, logic, options)` from `@microsoft/agents-hosting-directline-namedpipes` |

`AgentClient` handles outbound agent calls; the response controller handles callbacks and conversation correlation. Replace the old `/api/skills` flow as follows:

- Set `<AgentName>_endpoint` to the target agent's normal activity endpoint, usually `/api/messages`, and `<AgentName>_clientId` to its client ID.
- Set `<AgentName>_serviceUrl` to the calling agent's callback base, usually `/api/agentresponse`. `AgentClient` sends this URL and an `x-ms-conversation-id` header with the outbound activity.
- Register `configureResponseController(...)` on the calling agent. It owns `POST /api/agentresponse/v3/conversations/:conversationId/activities/:activityId`; do not recreate the old `/api/skills` route.

Verify both endpoint URLs, authentication, and the response-controller handler type against the planned stable package when the bot is also being rewritten to `AgentApplication`. Named pipes are Windows-only; preserve the configured pipe name.

## Teams

Register the extension and its routes together:

```ts
app.registerExtension(new TeamsAgentExtension(app), teams => {
  teams.onMessageEdit(async (context, state) => { /* migrated behavior */ })
})
```

| Status | Bot Framework override/helper | Agents SDK target |
|---|---|---|
| Rewrite | `TeamsActivityHandler` | `TeamsAgentExtension` registered on `AgentApplication` |
| Rewrite | message edit/delete/undelete | `teams.onMessageEdit` / `onMessageDelete` / `onMessageUndelete` |
| Rewrite | Teams members added/removed | `teams.onTeamsMembersAdded` / `onTeamsMembersRemoved` |
| Rewrite | channel and team lifecycle overrides | matching `teams.onTeamsChannel*` / `onTeamsTeam*` route |
| Rewrite | messaging extension query/select/link/task/action | matching `teams.messageExtension.on*` route |
| Rewrite | task module fetch/submit | `teams.taskModule.onFetch*` / `onSubmit*` |
| Rewrite | meeting events | matching `teams.meeting.on*` route |
| Direct | `TeamsInfo` member, team, channel, meeting helpers | `TeamsInfo` from the Teams extension package |

Preserve Teams manifest command IDs, task-module verbs, invoke envelopes, and response status/body.

## Unsupported and design decisions

Do not migrate these items automatically. **Unsupported** items have no verified stable target. **Decision** items require the user to choose a supported redesign first.

| Status | Bot Framework feature | Report |
|---|---|---|
| Unsupported | LUIS, QnA Maker, Orchestrator, legacy AI packages | no one-to-one stable Agents SDK target |
| Unsupported | Adaptive Dialogs, Composer `.dialog`, LG, LU | no direct Agents SDK migration |
| Decision | legacy Application Insights middleware | user must choose verified Agents telemetry/OpenTelemetry behavior |
| Unsupported | `TestAdapter`, `TestFlow`, `botbuilder-testing` | no public one-to-one test package |
| Unsupported | queue storage and inspection middleware | no direct equivalent |
| Unsupported | WebSocket and other legacy streaming transports except named pipes | no direct equivalent; migrate named pipes with `@microsoft/agents-hosting-directline-namedpipes` |
| Decision | `OAuthPrompt` | user must approve a redesign using verified authorization routes |
| Unsupported | Bot Framework CLI/generators, `TemplateManager`, deprecated activity types | no runtime migration target |
| Unsupported | unfamiliar or missing API after declaration/sample checks | retain and report; never synthesize a target |

For each row, report the source file and affected behavior. For **Decision**, also list the verified supported choices. For **Unsupported**, retain the code and dependency and provide no invented implementation.

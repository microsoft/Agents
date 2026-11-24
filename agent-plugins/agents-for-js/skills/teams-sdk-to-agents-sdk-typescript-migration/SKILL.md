---
name: teams-sdk-to-agents-sdk-typescript-migration
description: Migrate a JavaScript or TypeScript application using the Microsoft Teams SDK App runtime to Microsoft 365 Agents SDK while preserving its Teams behaviors. Use for Teams SDK migrations, including automated sample synchronization; use the separate Bot Framework migration skill for botbuilder applications.
---

# Teams SDK to Agents SDK for TypeScript

## Overview

Migrate the Teams SDK App runtime to Agents SDK AgentApplication. For applications
using botbuilder, use the Bot Framework migration skill instead. Honor read-only
review requests and caller-provided validation tools.

## Core rules

- Preserve commands, text matching, responses and state transitions, except that
  user-facing references to the migrated runtime must say "agent", not "bot".
  Apply this to README text, catalog descriptions, greetings and other messages.
  Keep platform terms such as Azure Bot, bot ID and `bots[]`, and preserve existing
  source-derived paths and identifiers unless the caller requests a rename.
- Preserve explicit customer SDK selections and authentication models. Do not
  introduce a new major version or prerelease without an applicable policy.
- Verify signatures and payloads against the installed selected-version types
  and implementation; similarly named APIs across SDKs are not interchangeable.
- Use the application's logging conventions, include operation context and the
  full Error, and avoid logging credentials or sensitive payloads.

Read the source handlers, startup, dependencies, environment configuration and
manifest before changing code. Inventory each command, invoke payload, response,
state transition and external call. Preserve command precedence and visibility,
including targeted versus public messages. A similarly named .NET API is not
evidence that a TypeScript API exists.

## Packages and runtime

Use stable published Agents SDK packages compatible with one another:

- `@microsoft/agents-activity`: Activity models and protocol helpers.
- `@microsoft/agents-hosting`: AgentApplication, state and storage.
- `@microsoft/agents-hosting-express`: Express startup.
- `@microsoft/agents-hosting-extensions-msteams`: Teams routes and client helpers.

Remove `@microsoft/teams.apps` and its runtime plugins. Supporting Teams card/model
packages can remain when used intentionally; the MSTeams extension itself depends
on `@microsoft/teams.api`. Do not globally ban every package in that namespace.
Avoid the deprecated `agents-hosting-extensions-teams` package for new migrations.

For synchronization, `context.migration.packages` is the frozen exact npm package
map. Declare those versions directly. The minimum is a floor, not the version to
copy. Do not translate NuGet floating versions into npm syntax. Node 24 is the
sync runtime; inspect published engines for customer applications.

## Migration steps

1. Inventory source handlers, dispatch precedence, payloads, state, external calls
   and setup. Record behaviors that need real Teams or tenant validation.
2. Scaffold the caller's destination, preserving existing layout and assets.
   In sample sync, use the configured destination and factory/startup convention below.
3. Replace dependencies and bootstrap with the selected Agents runtime. Keep
   server startup separate from the application factory and preserve static pages.
4. Migrate handlers using the mappings below; test actual dispatch and response
   envelopes, including unknown commands, empty inputs and failure paths.
5. Migrate configuration without changing the authentication model. Use an ignored
   `.env` for local credentials and a checked-in template with placeholders. Keep
   identity values consistent across connections, manifest placeholders and Graph
   configuration. Initialize optional external clients lazily so missing credentials
   do not prevent local startup; report missing configuration when the feature is used.
6. Reconcile the manifest using `teams-app-manifest`. Preserve valid metadata,
   use approved placeholders, and do not guess scopes, domains or permissions.
   Report unresolved decisions as `needs-policy` in automated sync. Return manifest
   evidence in the caller's report; do not create `manifest-evidence.md` by default.
7. Verify the final candidate as described below, separating local evidence from
   portal, identity, permission and live Teams checks.

## Application and route mapping

Create an `AgentApplication` with explicit storage. Keep an importable agent
factory separate from `startServer` so contracts can run routes without opening a
port. For Teams-specific behavior, construct `TeamsAgentExtension(app)` and call
`app.registerExtension(teams, callback)`; the callback is required. Inspect the
selected package's declarations and implementation before choosing signatures.

Typical mappings in the 1.9 SDK family:

| Teams SDK behavior | Agents SDK surface |
| --- | --- |
| `app.message` / `app.on('message')` | `app.onMessage` or `onTeamsMessage` |
| `context.send` | `context.sendActivity(Activity.fromObject(...))` for structured activity; strings can be sent directly |
| `card.action` | `app.adaptiveCards.actionExecute`; inspect the callback's action object and its `data` |
| `message.ext.query` | `teams.messageExtensions.onQuery(commandId, handler)` |
| `message.ext.query-link` | `teams.messageExtensions.onQueryLink` |
| `dialog.open` / `dialog.submit` | `teams.taskModules.onFetch` / `onSubmit` |
| `file.consent` | `teams.fileConsent` acceptance/decline routes |
| Meeting events | `teams.meetings` registration helpers |
| Feedback invoke | `onTeamsFeedbackLoop` |
| Teams API calls | `TeamsTurnContext.client` or `teams.getTeamsClient(context)` |
| Targeted activity | `TeamsTurnContext.sendTargetedActivity` and Activity targeting helpers |

Route ranks sort from smallest to largest. `RouteRank.Unspecified` is very large;
a numeric fallback such as 1000 can unexpectedly run first. Use explicit ranks
for competing routes and `RouteRank.Last` for catch-alls. Preserve existing
if/else precedence and unknown-command handling.

Ordinary `onActivity` routes receive a base `TurnContext`. Construct
`new TeamsTurnContext(context)` before using `.client`; a TypeScript cast does
not add the runtime getter. Message selectors that inspect text can skip
attachment-only messages; use `onActivity('message', ...)` when those are valid.

In 1.9.1, Activity parsing drops extra nested account/reaction fields, including
Teams' `recipient.isTargeted` and `reaction.user`. Preserve required values at
the HTTP boundary before parsing, using retained entities. Use
`Activity.makeTargetedActivity()` with a group conversation and explicit recipient
for private group delivery; preserve `targetedMessageInfo` for slash prompt preview.
Clone a captured conversation reference before normalizing its ID: the SDK can
retain references to incoming activity objects.

An Agents `Activity` is a class, not a plain Teams SDK message object. Convert
structured replies with `Activity.fromObject`, or use MessageFactory/CardFactory.
Keep Adaptive Card execute responses, task responses and compose-extension
responses in their proper envelopes. Sending a normal reply does not replace the
required invoke response.

For lifecycle events, AI metadata, Graph authentication and long-running invokes,
read [feature migration guidance](references/feature-migration.md) when those
features appear in the source. It describes behavior to preserve, not .NET APIs
or a requirement to add features the source does not have.

## State, external services and assets

Keep sample-local state scoped to the application factory. MemoryStorage and
in-memory reminders are development examples and reset at process restart.
Preserve timer cancellation, upload cleanup and targeted delivery semantics.
Inject external services where useful so tests can cover behavior without
Graph, file-transfer, Wikipedia or Teams credentials.

Use the Agents connection configuration conventions instead of copying Teams
runtime authentication bootstrap. Preserve delegated versus app-only Graph access using the appropriate configured
connection/authorization helper (see the feature reference). Never put secrets into manifests or test data.
Keep static task-module pages available under the URLs returned by handlers.

After code migration, use the standalone `teams-app-manifest` skill to reconcile
source, README/help and actual manifest JSON. Check exact command IDs/titles,
surfaces, task pages, file support and link domains. Do not assume schema validity
proves feature coverage.
Meeting event handlers also require manifest RSC grants and `webApplicationInfo`;
participant events require bot registration subscriptions in the Teams Developer
Portal. Verify these prerequisites separately from injected route tests.

## Verification

Outside automation, install dependencies using the project's package manager,
compile/typecheck the application, and run its behavior tests. Start the server
with the documented configuration and verify its health endpoint (normally
`GET /` on port 3978). Check required static pages as well as startup. Run the
manifest skill's schema, asset and capability checks. A successful build does not
prove routing, invoke envelopes or tenant behavior.

In automation, use the supplied validation tool instead of requesting unavailable
shell access. Require current full validation after the last change. Tests must
preserve Agents-specific behavior, including terminology; matching upstream text
verbatim is not a reason to replace "Agent" with "Bot" or weaken an existing test.
Report failed, blocked and untested checks explicitly.

## Common mistakes

| Mistake | Correction |
| --- | --- |
| Copying upstream "Bot" greetings | Preserve the greeting's intent with "Agent" terminology |
| Treating a TypeScript cast as a runtime wrapper | Construct TeamsTurnContext where required |
| Letting a catch-all shadow commands | Verify route ranks and overlapping selectors |
| Sending a normal reply instead of an invoke response | Use the proper route and response envelope |
| Waiting for a long upload before invoke acknowledgment | Queue work and use proactive continuation |
| Replacing app-only Graph calls with delegated calls | Preserve the original permission model |
| Copying .NET AI models or rejecting all plain JS entities | Verify JS helpers and serialized wire fields |
| Considering schema validity sufficient | Reconcile implemented capabilities and run behavioral tests |

## Automated sample synchronization

Follow the caller's read-only planning and frozen-plan protocol. Only edit the
selected sample; protected contracts and automation are outside its write scope.
Use `validate_sample` for dependency preparation, compilation, contracts, manifest
and startup checks. The trusted tool generates the lockfile with npm; do not
invent lockfile integrity values or request unrestricted shell access.

Keep package scripts explicit and finite, with no recursive npm install hooks.
Export `createAgent` from `src/agent.ts`, start the server in `src/index.ts`, and
compile strict NodeNext source from `src` to `dist`. Retain a credential-free
health endpoint. Report unsupported behavior as a concrete blocker with source
evidence rather than omitting it or claiming the migration passed.

Official source: <https://github.com/microsoft/Agents-for-js>. The installed
selected package types and source are authoritative for the run's API surface.

## Repository manifest identity convention

For this repository, use `${{AAD_APP_CLIENT_ID}}` consistently for application client ID placeholders in the Teams manifest, including `id`, `bots[].botId` and `composeExtensions[].botId`. Preserve unrelated suffix and domain placeholders. Align setup documentation and provisioning substitutions. Keep source-derived sample identifiers; describe migrated runtimes as agents while preserving platform and schema names.

---
name: teams-sdk-to-agents-sdk-python-migration
description: Migrate Python Microsoft Teams SDK application runtimes to Microsoft 365 Agents SDK while preserving Teams routing, invokes, proactive delivery, and sample behavior. Use for Teams SDK migrations and automated sample synchronization, not generic Python development.
---

# Teams SDK to Agents SDK for Python

Inventory handlers, precedence, payloads, response envelopes, state, external calls,
configuration, static pages, and manifests before editing. Preserve observable behavior.
Use agent terminology for the migrated runtime while retaining platform names such as
Azure Bot and protocol keys such as `botId`. Honor read-only requests.

## Runtime and packages

Use the caller's frozen SDK selection. For synchronization, declare every exact version
from `context.migration.packages`; the minimum is a floor, not the selected version.
Do not downgrade, remove a capability, or suppress diagnostics to obtain a passing build.

| Distribution | Import / responsibility |
| --- | --- |
| microsoft-agents-activity | `microsoft_agents.activity`: activity models and environment configuration |
| microsoft-agents-hosting-core | `microsoft_agents.hosting.core`: application, storage, context, authorization |
| microsoft-agents-hosting-aiohttp | `microsoft_agents.hosting.aiohttp`: CloudAdapter and HTTP processing |
| microsoft-agents-authentication-msal | `microsoft_agents.authentication.msal`: connection manager |
| microsoft-agents-hosting-msteams | `microsoft_agents.hosting.msteams`: TeamsAgentExtension and Teams context |

The 1.9.0 Teams extension requires Python >=3.11, although the other four distributions
declare >=3.10. Check the entire resolved dependency set and wheel availability for the
selected runtime. `microsoft-teams-api` is a legitimate extension dependency; do not ban
all Teams packages. Remove obsolete Teams application runtime imports and dependencies.
Do not substitute the older similarly named hosting-teams distribution.

## Application construction and hosting

For the 1.9.0 selection, first read [released API pitfalls](references/sdk-1.9.md).
It records the constructor, injection, readiness, invoke and Graph failures found by real
synchronization contracts, with source locations inside the released distributions.
Do not guess repository directory layouts or release-tag URLs as substitutes for those APIs.

Use the maintained Python reference configured by `canonicalSample` as the local hosting reference. Construct
MemoryStorage, MsalConnectionManager, CloudAdapter, Authorization, and AgentApplication
with compatible selected-version signatures and the existing connection configuration.
Preserve authentication models and never embed secrets in code or manifest assets.

Expose an importable `create_agent` factory. Keep state and injected service dependencies
inside each application instance. Put server startup behind an explicit entry point;
importing application modules must not bind ports, create timers, or start network work.
Provide explicit startup/cleanup for background tasks and pending uploads.

For synchronized samples, use `create_agent(dependencies=None)` returning AgentApplication.
Accept a `connection_manager` entry in that dependency mapping so protected contracts can
reject unexpected credential requests while exercising real SDK dispatch. Keep ordinary
startup configuration as the default when that entry is absent.
Accept an `adapter` override for offline outbound and proactive transport checks. Background
upload/reminder work must use the configured adapter's supported continuation API, rather
than sending through an expired incoming turn context.
Dialog samples also accept an `endpoint` override for local protected checks. The released
task-module selectors read `activity.value.data.verb`; preserve source action identifiers
such as `adaptive_card`, `custom_form`, and `multi_step_form` in that field, and carry form
values alongside the verb. Serve the source's `/customform` route and keep fallbackUrl aligned.
Message-extension samples accept an async `search_wikipedia(query)` dependency; the default
implementation performs the real Wikipedia request. Keep SDK query dispatch and response
serialization active when injecting the external search service. Register an explicit
unknown-command fallback that returns the no-results envelope.
Keep the default helper as `src.wikipedia.search_wikipedia(query)` using `httpx`.
Validate JSON root/query/search shapes and handle HTTP/JSON failures by returning
an empty list, preserving upstream's no-results behavior. Protected tests exercise
the real helper through an HTTPX mock transport, alongside injected route tests.
Attachment samples accept async `download(url)` and `upload(url, content)` dependencies.
Keep default HTTP helpers in `src/files.py` as async `download_attachment(url)` and
`upload_to_onedrive(url, content)`. Preserve byte content, Content-Type, Content-Length,
Content-Range and failure status handling; protected checks exercise actual local HTTP.
Expose `await app.wait_for_uploads()` to drain tracked background work in local contracts;
normal server shutdown must drain or cancel that same work. Transfer failures must be handled
and cleared rather than escaping as unobserved background-task exceptions.
Meeting samples accept async `meeting_info(context, id)`, `transcript(context, resource, user)`
and `wait(seconds)` dependencies. Preserve a 30-second initial transcript delay followed by
10-second retries, with three attempts. Use ordinary async sleep by default; injected waits
make protected routing checks deterministic. Keep default Graph transport separately testable.
Keep `get_meeting_transcript(graph_client, resource_id, user_id)` and `parse_vtt(text)` in
`src/transcripts.py`. The transcript helper must select the newest metadata entry and retrieve
its content with Accept: text/vtt through the beta users/onlineMeetings/transcripts route.
Verify generated Graph request URLs, including their base URL, against the installed Graph
and Kiota versions; passing the right resource IDs is not sufficient if the builder emits
a relative or incorrectly versioned URL.
Targeted-message samples accept `now()` in seconds, `schedule(async_callback, seconds)`
returning a cancellation callable, and async `reaction(context, operation, message_id, type)`.
The default scheduler uses asyncio and tracks cancellation. Expose idempotent
`await app.close_background_tasks()` for server shutdown and protected test cleanup.

For aiohttp, retain the authorization middleware, application configuration/adapter keys,
and `POST /api/messages` processing through `start_agent_process`. Add a credential-free
`GET /` readiness response without bypassing authentication for message requests.
Serve custom dialog pages at the URLs returned by dialog handlers.

Canonical inputs are fingerprinted synchronization inputs. Copy needed hosting code only
into the selected sample. Report missing/incompatible canonical helpers as prerequisites;
do not fetch unpinned replacements or modify sibling samples from a migration session.

## Teams route mapping

In the published 1.9.0 extension, `TeamsAgentExtension(app)` attaches route namespaces and
a before-turn hook. Python does not use the TypeScript registerExtension callback pattern.
Inspect the installed wheel for the selected release before using signatures or models.

| Behavior | Python surface to inspect |
| --- | --- |
| Messages and activity fallback | `app.message`, `app.activity`, or Teams extension wrappers |
| Compose search | `teams.message_extensions.query(command_id)` |
| Link unfurl | `teams.message_extensions.query_link` |
| Dialog fetch and submit | `teams.task_modules.fetch`, `teams.task_modules.submit` |
| Adaptive Card execute | `app.adaptive_card` action registration, verified against the selected wheel |
| Feedback submission / feedback dialog | `app.invoke('message/submitAction')` / `app.invoke('message/fetchTask')` |
| File consent | `teams.file_consent.accept`, `teams.file_consent.decline` |
| Meeting events | `teams.meetings.start`, `end`, `participants_join`, `participants_leave` |
| Teams API transport | `teams.get_teams_api_client(context)` |
| Graph transport | extension Graph client helpers, preserving delegated/app-only intent |

Use explicit import aliases, application/extension construction, and literal or constant
command selectors so trusted AST inspection can reconcile manifest capabilities. Report
unsupported dynamic registration instead of hiding it from validation.

Teams wrappers deserialize typed invoke values and send responses returned by handlers.
The 1.9.0 Python message namespace has no dedicated feedback or fetch-task helper. Use
explicit invoke routes for these activities, validate the value payload, and send the
required invokeResponse activity explicitly; generic `app.invoke` does not convert a
returned dictionary into a Teams response. Preserve Python's custom feedback dialog and
`feedbackLoop.type = custom` when migrating the AI messages source.
Preserve each invoke's expected envelope and acknowledgement; sending an ordinary message
is not an invoke response. Test actual SDK dispatch, including route precedence, rather
than calling only application handlers. Read [behavior preservation](references/behaviors.md)
for the sample families present in the source.

## Assets and validation

Keep each sample independently runnable with dependency declarations, resolver-produced
hash locks, environment template, README, manifest, icons, and required web assets.
The README's reproducible setup must install the validated lock with
`python -m pip install --require-hashes -r requirements.lock`, not install floating
declarations from `requirements.txt`. Explain the Python runtime and platform used
to produce the lock; wheel hashes can be platform-specific. A different environment
needs its own trusted resolution and validation rather than a claim that the same
wheel lock is portable. Include fresh virtual-environment setup before installation.
Reuse the shared `teams-app-manifest` skill for schema/assets/capability reconciliation.
Check exact command IDs, slash/mention/dialog surfaces, link domains, file support,
meeting grants and setup instructions. Schema validity alone does not prove parity.
For these seven repository synchronization targets, read
[existing installation-scope evidence](references/manifest-scopes.md) before declaring
scopes unresolved. The same-family .NET and JS manifests provide repository parity
evidence; a generic handler alone does not establish intended installation scopes.

Within synchronization, follow the shared `sync-teams-samples-to-agents-sdk` skill and its
read-only planning, frozen plan, selected-sample confinement, self-audit, and bounded repair
protocol. Use the supplied `validate_sample` tool; trusted tooling owns dependency locking
and environment preparation. Never fabricate hashes, installed evidence, or successful checks.
Require current full validation after the last edit, including dependencies, compilation,
import, protected routing contracts, manifest, and HTTP readiness.
Call `validate_sample` sequentially and wait for each result before starting another
validation or editing files. Concurrent calls are rejected and edits make evidence stale.

Outside synchronization, use the project's documented environment setup, compile/import
checks, and behavior tests. Run each application in its own environment. Separate local
mocked-transport results from live installation/authentication, Graph permissions,
transcripts, file transfer and targeted delivery checks. Report pending checks explicitly.

Official SDK source: <https://github.com/microsoft/Agents-for-python>. The selected published
wheel is authoritative for release-specific APIs.

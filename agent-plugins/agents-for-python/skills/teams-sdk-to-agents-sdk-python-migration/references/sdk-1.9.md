# Released Python SDK 1.9.0 implementation notes

These notes were checked against installed 1.9.0 distributions and the protected
contracts. Apply them to the frozen 1.9.0 selection; recheck APIs for another release.
Source paths below are relative to the installed environment's site-packages,
not guessed paths in the SDK GitHub repository.

## Application construction and injected dependencies

`microsoft_agents/hosting/core/app/agent_application.py` accepts keyword-only
`connection_manager` or `authorization`. Supply one, not both. The public
`connection_manager` property has no setter. An adapter alone is insufficient.

```python
def create_agent(dependencies=None):
    deps = dependencies or {}
    manager = deps.get("connection_manager")
    if manager is None:
        manager = MsalConnectionManager(**configuration)
    adapter = deps.get("adapter")
    if adapter is None:
        adapter = CloudAdapter(connection_manager=manager)
    app = AgentApplication(
        storage=MemoryStorage(), adapter=adapter, connection_manager=manager
    )
    teams = TeamsAgentExtension(app)
    # Register handlers here and return app.
```

Never use `deps.get("connection_manager", MsalConnectionManager(**configuration))`:
Python evaluates the default constructor even when the injected manager exists.
Never assign `app.connection_manager = ...`. Keep the chosen manager and adapter
consistent across authorization, application construction and background continuation.
The offline harness supplies a manager that rejects actual credential requests.

## Credential-free readiness, authenticated messages

Do not construct an MSAL manager with absent configuration before the host can
serve `GET /`. Create the credential-dependent agent lazily for message processing
or use a supported explicit configuration. Retain the canonical JWT authorization
for `/api/messages`; exposing readiness must not make message processing anonymous.
If wrapping middleware to bypass readiness, match exactly `GET /` and delegate
every other request to the SDK JWT middleware. Do not catch startup errors and
silently substitute an unauthenticated message host.

`load_configuration_from_env` returns a nested dictionary, not an
`AgentAuthConfiguration`. The SDK JWT middleware expects the typed object in
`http_app["agent_configuration"]`; use the connection manager's
`get_default_connection_configuration()` result, not the whole environment mapping.
Passing a dictionary raises `AttributeError` on properties such as
`ANONYMOUS_ALLOWED` and returns HTTP 500 even for anonymous messages.
The protected smoke check now starts separate hosts to prove readiness without
credentials and HTTP 401/403 rejection of anonymous activity with synthetic local
configuration. Preserve that distinction and correct configuration errors rather
than accepting HTTP 500 as proof of authorization.

## Literal feedback invoke registration

Register the AI sample's two protocol names directly inside `create_agent`:

```python
@app.invoke("message/submitAction")
async def on_feedback_submit(context, state):
    await handle_feedback_submit(context, state)

@app.invoke("message/fetchTask")
async def on_feedback_fetch(context, state):
    await handle_feedback_fetch(context, state)
```

The sample supplies these two handler functions and must send the appropriate
`invokeResponse` activity explicitly from each. Do not replace these literal
registrations with `app.add_route(predicate, handler, is_invoke=True)`; the trusted
AST inspector rejects predicate-based dynamic registrations because it cannot
reconcile their protocol names with manifest capabilities. The generic invoke
API supports both names in 1.9.0; unavailable feedback-specific namespace helpers
are not a reason to use dynamic registration or omit the dialogs.

## Unknown message-extension commands

The Teams namespace handles configured command IDs. An unregistered ID still
needs the protected no-results response. In the released core application,
`app.invoke()` with no selector registers a last-ranked invoke fallback. Filter
that fallback by `context.activity.name == "composeExtension/query"`, then send
one `invokeResponse` with status 200 and a `composeExtension` message containing
`{"type": "message", "text": "No results found for '<query>'"}`. Extract the query
from the invoke parameters, as the recognized command does for empty search results.
This preserves the pinned source's no-results message and the protected contract;
an empty result-list envelope is not equivalent. The source's unknown-command path
has an uninitialized variable, so extend the no-results behavior to that path.
Do not return a bare dictionary from a generic core invoke handler and assume it
will be serialized: unlike the Teams wrapper, that handler must send the response.
Specific registered commands must retain precedence over the fallback.

## Graph transcripts and manifest

The Graph client's request adapter exposes its base URL. Ensure transcript metadata
and content requests use that base URL and the complete users/onlineMeetings path.
A relative URL alone fails the protected transport contract. Preserve the configured
Graph API version and select the newest transcript before requesting content with
`Accept: text/vtt`. Verify the actual request URL through the protected test; using
the correct identifiers alone does not establish correct transport.

Every bot manifest requires `bots[].scopes`. Derive scopes from the source,
configured manifest policy, and sample behavior; do not omit the property when
uncertain. Resolve missing scope evidence in the frozen plan. Keep compose command
IDs and declared domains aligned with the actual registered routes.

Run the complete protected suite after repairs. Fixing factory construction only
removes the first blocker; attachment transfers, reminders, invokes and Graph
behavior still need to pass their individual assertions.

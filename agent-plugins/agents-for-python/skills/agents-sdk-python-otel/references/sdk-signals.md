# Agents SDK telemetry signals for Python

This self-contained reference is the compact telemetry checklist for Python
agents.

## Integration model

The Agents SDK resolves OpenTelemetry tracers and meters at module import time.
Configure providers before importing most `microsoft_agents` packages.
`microsoft_agents.activity` is safe to import before setup.

Span and metric constants live in telemetry modules under
`microsoft_agents.hosting.core`. Import those constants only after providers
are configured.

## Common spans

| Area | Spans |
|---|---|
| Adapter | `agents.adapter.process`, `agents.adapter.write_response`, `agents.adapter.send_activities`, `agents.adapter.update_activity`, `agents.adapter.delete_activity`, `agents.adapter.continue_conversation`, `agents.adapter.create_connector_client`, `agents.adapter.create_user_token_client` |
| AgentApplication | `agents.app.run`, `agents.app.route_handler`, `agents.app.before_turn`, `agents.app.after_turn`, `agents.app.download_files` |
| Turn context | `agents.turn.send_activities` |
| Connector | `agents.connector.reply_to_activity`, `agents.connector.send_to_conversation`, `agents.connector.update_activity`, `agents.connector.delete_activity`, `agents.connector.create_conversation`, `agents.connector.get_conversations`, `agents.connector.get_conversation_members`, `agents.connector.upload_attachment`, `agents.connector.get_attachment_info`, `agents.connector.get_attachment` |
| Storage | `agents.storage.read`, `agents.storage.write`, `agents.storage.delete` |
| User token client | `agents.user_token_client.get_user_token`, `agents.user_token_client.sign_out`, `agents.user_token_client.exchange_token`, `agents.user_token_client.get_token_or_sign_in_resource`, `agents.user_token_client.get_token_status`, `agents.user_token_client.get_aad_tokens` |
| Authentication | `agents.authentication.get_access_token`, `agents.authentication.acquire_token_on_behalf_of`, `agents.authentication.get_agentic_instance_token`, `agents.authentication.get_agentic_user_token` |
| Authorization | `agents.authorization.agentic_token`, `agents.authorization.azure_bot_token`, `agents.authorization.azure_bot_signin`, `agents.authorization.azure_bot_signout` |

SDK spans set `ERROR`, record propagated exceptions, and re-raise them.

## Common metrics

| Metric | Type | Unit |
|---|---|---|
| `agents.adapter.process.duration` | Histogram | ms |
| `agents.activities.received` | Counter | activity |
| `agents.activities.sent` | Counter | activity |
| `agents.activities.updated` | Counter | activity |
| `agents.activities.deleted` | Counter | activity |
| `agents.turn.count` | Counter | turn |
| `agents.turn.error.count` | Counter | turn |
| `agents.turn.duration` | Histogram | ms |
| `agents.storage.operation.total` | Counter | operation |
| `agents.storage.operation.duration` | Histogram | ms |
| `agents.connector.request.count` | Counter | request |
| `agents.connector.request.duration` | Histogram | ms |
| `agents.user_token_client.request.count` | Counter | request |
| `agents.user_token_client.request.duration` | Histogram | ms |
| `agents.auth.token.request.count` | Counter | request |
| `agents.auth.token.duration` | Histogram | ms |

Python records `agents.turn.duration` on both success and error paths.

## Constants

After provider configuration, constants can be imported from:

```python
from microsoft_agents.hosting.core.telemetry.adapter import (
    constants as adapter_constants,
)
from microsoft_agents.hosting.core.app.telemetry import (
    constants as app_constants,
)
from microsoft_agents.hosting.core.connector.telemetry import (
    constants as connector_constants,
)
from microsoft_agents.hosting.core.storage.telemetry import (
    constants as storage_constants,
)
from microsoft_agents.hosting.core.authorization.telemetry import (
    constants as auth_constants,
)
```

Do not import these modules into the provider bootstrap solely to build a span
filter; use the documented literal names there to preserve initialization
order.

## Filtering

Prefer the OpenTelemetry Collector filter processor with OTTL for centralized
removal of selected spans before backend export. Use SDK sampling when the
goal is to reduce application recording and network overhead.

Dropping individual spans at either layer can fragment traces. Prefer
whole-trace sampling when possible, and do not use an application
`SpanProcessor` as the default filtering-policy mechanism.

## Custom telemetry attributes

Prefer bounded operational values:

- `activity.type`
- `activity.channel_id`
- `route.authorized`
- `route.matched`
- `route.is_invoke`
- `route.is_agentic`
- `storage.operation`
- `auth.method`
- `auth.success`
- `operation`
- `http.method`
- `http.status_code`
- `status`
- `error.type`

Do not add user, conversation, activity, prompt, response, attachment, token,
endpoint, or exception-message values to custom metrics.

## Aspire Dashboard smoke test

After one successful message, expect:

1. Telemetry grouped under the application's `service.name`.
2. An inbound `agents.adapter.process` trace.
3. For `AgentApplication`, `agents.app.run` and route-handler spans.
4. Activity and turn metrics.
5. Server/client HTTP spans for configured instrumentations.
6. Python logging records correlated with the active trace.

After one failed turn, expect:

1. An error-marked span with a recorded exception.
2. `agents.turn.error.count` to increase.
3. `agents.turn.duration` to record the failed turn.
4. The exception to continue through normal application handling.

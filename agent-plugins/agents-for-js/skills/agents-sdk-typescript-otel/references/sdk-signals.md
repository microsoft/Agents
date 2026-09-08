# Agents SDK telemetry signals for TypeScript and JavaScript

This self-contained reference is the compact telemetry checklist for
JavaScript and TypeScript agents.

## Integration model

The Agents SDK emits through the global OpenTelemetry APIs. Start a
`NodeSDK` before importing the Agents SDK or HTTP stack. No extra
Agents-specific instrumentation registration is required.

Span and metric constants are exported by `@microsoft/agents-telemetry`:

```typescript
import {
  MetricNames,
  SpanNames
} from '@microsoft/agents-telemetry'
```

Use constants when querying or referring to SDK signals in code. Do not
duplicate SDK spans with custom spans of the same name.

## Common spans

| Area | Spans |
|---|---|
| Adapter | `agents.adapter.process`, `agents.adapter.send_activities`, `agents.adapter.update_activity`, `agents.adapter.delete_activity`, `agents.adapter.continue_conversation`, `agents.adapter.create_connector_client`, `agents.adapter.create_user_token_client` |
| AgentApplication | `agents.app.run`, `agents.app.route_handler`, `agents.app.before_turn`, `agents.app.after_turn`, `agents.app.download_files` |
| Turn context | `agents.turn.send_activities` |
| Connector | `agents.connector.reply_to_activity`, `agents.connector.send_to_conversation`, `agents.connector.update_activity`, `agents.connector.delete_activity`, `agents.connector.create_conversation`, `agents.connector.get_conversations`, `agents.connector.get_conversation_member`, `agents.connector.upload_attachment`, `agents.connector.get_attachment_info`, `agents.connector.get_attachment` |
| Agent client | `agents.agent_client.post_activity` |
| Storage | `agents.storage.read`, `agents.storage.write`, `agents.storage.delete` |
| Authentication | `agents.authentication.get_access_token`, `agents.authentication.acquire_token_on_behalf_of`, `agents.authentication.get_agentic_instance_token`, `agents.authentication.get_agentic_user_token` |
| Authorization | `agents.authorization.agentic_token`, `agents.authorization.azure_bot_token`, `agents.authorization.azure_bot_obo_token`, `agents.authorization.azure_bot_signin`, `agents.authorization.azure_bot_signout` |
| Proactive | `agents.proactive.store_conversation`, `agents.proactive.get_conversation`, `agents.proactive.get_conversation_or_throw`, `agents.proactive.delete_conversation`, `agents.proactive.send_activity`, `agents.proactive.continue_conversation`, `agents.proactive.create_conversation` |
| Dialogs | `agents.dialogs.run` and `agents.dialogs.context.*` |
| Copilot Studio client | `agents.copilot_client.start_conversation`, `agents.copilot_client.send_activity`, `agents.copilot_client.post_request`, and streaming/subscription spans |

SDK spans record propagated errors, set error status, add a failure event, end
the span, and rethrow the error.

## Common metrics

| Metric | Type | Unit |
|---|---|---|
| `agents.activities.received` | Counter | activities |
| `agents.activities.sent` | Counter | activities |
| `agents.activities.updated` | Counter | activities |
| `agents.activities.deleted` | Counter | activities |
| `agents.turn.count` | Counter | turn |
| `agents.turn.error.count` | Counter | turn |
| `agents.turn.duration` | Histogram | ms |
| `agents.adapter.process.duration` | Histogram | ms |
| `agents.connector.request.count` | Counter | request |
| `agents.connector.request.duration` | Histogram | ms |
| `agents.agent_client.request.count` | Counter | request |
| `agents.agent_client.request.duration` | Histogram | ms |
| `agents.storage.operation.duration` | Histogram | ms |
| `agents.auth.token.request.count` | Counter | request |
| `agents.auth.token.duration` | Histogram | ms |
| `agents.user_token_client.request.count` | Counter | request |
| `agents.user_token_client.request.duration` | Histogram | ms |
| `agents.proactive.operation.count` | Counter | operation |
| `agents.proactive.operation.duration` | Histogram | ms |
| `agents.dialogs.context.count` | Counter | operation |
| `agents.dialogs.context.duration` | Histogram | ms |
| `agents.copilot_client.activities.received` | Counter | activities |
| `agents.copilot_client.activities.sent` | Counter | activities |
| `agents.copilot_client.conversations.started` | Counter | conversations |
| `agents.copilot_client.webchat.connection.count` | Counter | connections |
| `agents.copilot_client.request.count` | Counter | request |
| `agents.copilot_client.request.error.count` | Counter | request |
| `agents.copilot_client.request.duration` | Histogram | ms |
| `agents.copilot_client.stream.duration` | Histogram | ms |
| `agents.copilot_client.execute_streaming.count` | Counter | operation |
| `agents.copilot_client.subscribe_async.count` | Counter | operation |
| `agents.copilot_client.subscribe_event.count` | Counter | events |

## Span category filtering

Set `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` to a comma- or space-separated
list containing:

- `STORAGE`
- `AUTHENTICATION`
- `AUTHORIZATION`
- `DIALOGS`

All categories are enabled by default.

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
5. HTTP spans when HTTP instrumentation was preloaded.
6. Direct OpenTelemetry logs with trace and span IDs when emitted in an active
   context.

After one failed turn, expect:

1. An error-marked span with a recorded exception.
2. `agents.turn.error.count` to increase.
3. The application error to continue through its normal handling path.

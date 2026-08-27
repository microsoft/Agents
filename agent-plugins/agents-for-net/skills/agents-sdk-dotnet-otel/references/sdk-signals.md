# Agents SDK telemetry signals for .NET

This self-contained reference is the compact telemetry checklist needed while
configuring and validating a .NET agent.

## Instrumentation identity

The Agents SDK emits standard .NET diagnostics and does not depend directly on
OpenTelemetry:

```csharp
AgentsTelemetry.SourceName     // "Microsoft.Agents.Core"
AgentsTelemetry.ActivitySource
AgentsTelemetry.Meter
```

Subscribe with:

```csharp
tracing.AddSource(AgentsTelemetry.SourceName);
metrics.AddMeter(AgentsTelemetry.SourceName);
```

The application's `service.name` must identify the application, not the SDK
source.

## Common spans

| Area | Spans |
|---|---|
| Adapter | `agents.adapter.process`, `agents.adapter.write_response`, `agents.adapter.send_activities`, `agents.adapter.update_activity`, `agents.adapter.delete_activity`, `agents.adapter.continue_conversation` |
| AgentApplication | `agents.app.run`, `agents.app.route_handler`, `agents.app.before_turn`, `agents.app.after_turn`, `agents.app.download_files` |
| Turn context | `agents.turn.send_activities` |
| Connector | `agents.connector.reply_to_activity`, `agents.connector.send_to_conversation`, `agents.connector.update_activity`, `agents.connector.delete_activity`, `agents.connector.create_conversation`, `agents.connector.get_conversations`, `agents.connector.get_conversation_members`, `agents.connector.upload_attachment`, `agents.connector.get_attachment_info`, `agents.connector.get_attachment` |
| Storage | `agents.storage.read`, `agents.storage.write`, `agents.storage.delete` |
| Authentication | `agents.authentication.get_access_token`, `agents.authentication.acquire_token_on_behalf_of`, `agents.authentication.get_agentic_instance_token`, `agents.authentication.get_agentic_user_token` |
| Authorization | `agents.authorization.agentic_token`, `agents.authorization.azure_bot_token`, `agents.authorization.azure_bot_signin`, `agents.authorization.azure_bot_signout` |

SDK spans mark propagated exceptions as errors, add an `exception` event, and
rethrow the exception.

## Common metrics

| Metric | Type | Unit |
|---|---|---|
| `agents.activities.received` | Counter | activity |
| `agents.activities.sent` | Counter | activity |
| `agents.activities.updated` | Counter | activity |
| `agents.activities.deleted` | Counter | activity |
| `agents.turn.count` | Counter | turn |
| `agents.turn.error.count` | Counter | turn |
| `agents.turn.duration` | Histogram | ms |
| `agents.adapter.process.duration` | Histogram | ms |
| `agents.connector.request.count` | Counter | request |
| `agents.connector.request.duration` | Histogram | ms |
| `agents.user_token_client.request.count` | Counter | request |
| `agents.user_token_client.request.duration` | Histogram | ms |
| `agents.storage.operation.total` | Counter | operation |
| `agents.storage.operation.duration` | Histogram | ms |
| `agents.auth.token.request.count` | Counter | request |
| `agents.auth.token.request.duration` | Histogram | ms |

`agents.turn.duration` is recorded only for successful turns.

## Useful bounded attributes

Prefer the public constants in `Microsoft.Agents.Core.Telemetry.TagNames` when
adding compatible custom telemetry. Common bounded attributes include:

- `activity.type`
- `activity.channel_id`
- `activity.delivery_mode`
- `activity.is_agentic_request`
- `route.authorized`
- `route.matched`
- `route.is_invoke`
- `route.is_agentic`
- `storage.operation`
- `auth.method`
- `auth.success`
- `http.method`
- `http.status_code`

Identifiers such as user, conversation, activity, attachment, handler, app,
service URL, or token-service endpoint may be useful on carefully controlled
traces, but must not be copied to metrics and should be excluded by default.

## Aspire Dashboard smoke test

After one successful message, expect:

1. A resource named for the application.
2. An inbound adapter trace rooted at `agents.adapter.process`.
3. For `AgentApplication`, an `agents.app.run` child operation and route spans.
4. Activity and turn counters.
5. ASP.NET Core and HTTP client telemetry when those operations occur.
6. Structured application logs with trace and span IDs when emitted inside an
   active trace.

After one failed turn, expect:

1. An error-marked span with an exception event.
2. `agents.turn.error.count` to increase.
3. The application's normal exception handling to remain unchanged.

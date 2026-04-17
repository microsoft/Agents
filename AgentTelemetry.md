# M365 Agents SDK Telemetry

Distributed traces, metrics, and span attributes for the M365 Agents SDK across C#, Python, and JavaScript.

The M365 Agents SDK provides built-in instrumentation to help developers monitor and debug their agent applications. Each SDK emits traces and metrics for key operations using the standard observability primitives of its runtime — `System.Diagnostics` in C#, and OpenTelemetry in Python and JavaScript. All signals are compatible with the OpenTelemetry ecosystem.

---

## Quick Navigation

### JavaScript Telemetry
- [Traces (Spans)](#javascript-spans)
  - [CloudAdapter Spans](#cloudadapter-spans)
  - [AgentApplication Spans](#agentapplication-spans)
  - [TurnContext Spans](#turncontext-spans)
  - [ConnectorClient Spans](#connectorclient-spans)
  - [AgentClient Spans](#agentclient-spans)
  - [Storage Spans](#storage-spans)
  - [Authentication Spans](#authentication-spans)
  - [Authorization Spans](#authorization-spans)
  - [UserTokenClient Spans](#usertokenclient-spans)
  - [Error Handling in Spans](#error-handling-in-spans)
  - [Disabling Span Categories](#disabling-span-categories)
- [Metrics](#javascript-metrics)
  - [Activity Counters](#activity-counters)
  - [Request Counters](#request-counters)
  - [Turn Counters](#turn-counters)
  - [Duration Histograms](#duration-histograms)

### C# Telemetry
- [System.Diagnostics Integration](#systemdiagnostics-integration)
- [Setup](#c-setup)
- [Traces (Spans)](#c-spans)
  - [CloudAdapter Spans](#c-cloudadapter-spans)
  - [AgentApplication Spans](#c-agentapplication-spans)
  - [TurnContext Spans](#c-turncontext-spans)
  - [ConnectorClient Spans](#c-connectorclient-spans)
  - [UserTokenClient Spans](#c-usertokenclient-spans)
  - [Storage Spans](#c-storage-spans)
  - [Authentication Spans](#c-authentication-spans)
  - [Authorization Spans](#c-authorization-spans)
  - [Error Handling in Spans](#c-error-handling-in-spans)
- [Metrics](#c-metrics)
  - [Activity Counters](#c-activity-counters)
  - [Turn Metrics](#c-turn-metrics)
  - [Adapter Metrics](#c-adapter-metrics)
  - [Connector Metrics](#c-connector-metrics)
  - [UserTokenClient Metrics](#c-usertokenclient-metrics)
  - [Storage Metrics](#c-storage-metrics)
  - [Authentication Metrics](#c-authentication-metrics)
- [Span Constants Reference](#c-span-constants-reference)
- [Attribute Constants Reference](#c-attribute-constants-reference)

### Python Telemetry
- [Setup](#python-setup)
- [Traces (Spans)](#python-spans)
  - [Adapter Spans](#python-adapter-spans)
  - [AgentApplication Spans](#python-agentapplication-spans)
  - [TurnContext Spans](#python-turncontext-spans)
  - [ConnectorClient Spans](#python-connectorclient-spans)
  - [Storage Spans](#python-storage-spans)
  - [UserTokenClient Spans](#python-usertokenclient-spans)
  - [Authentication Spans](#python-authentication-spans)
  - [Authorization Spans](#python-authorization-spans)
- [Metrics](#python-metrics)
  - [Adapter Metrics](#python-adapter-metrics)
  - [Turn Metrics](#python-turn-metrics)
  - [Storage Metrics](#python-storage-metrics)
  - [Connector Metrics](#python-connector-metrics)
  - [UserTokenClient Metrics](#python-usertokenclient-metrics)
  - [Authentication Metrics](#python-authentication-metrics)
- [Disabling Span Categories](#python-disabling-span-categories)
- [Error Handling in Spans](#python-error-handling-in-spans)
- [Span Constants Reference](#python-span-constants-reference)
- [Metric Constants Reference](#python-metric-constants-reference)

---

## Using This Document

This document describes the telemetry signals emitted by the M365 Agents SDK. Use this reference to:

1. **Understand what data is collected** - Each span and metric is documented with its name, attributes, and purpose.
2. **Build dashboards and alerts** - Use the metric and span names to create observability dashboards in tools like Azure Monitor, Jaeger, or Grafana.
3. **Debug issues** - Trace spans help identify where latency or errors occur in your agent's request processing pipeline.
4. **Ensure consistency** - Multi-language implementations follow the same naming conventions for spans and metrics.

### Enabling OpenTelemetry

To enable telemetry in your agent application, ensure you have the OpenTelemetry SDK configured:

**JavaScript:**
```bash
npm install @opentelemetry/api @opentelemetry/sdk-node
```

```typescript
import { NodeSDK } from '@opentelemetry/sdk-node';
import { ConsoleSpanExporter } from '@opentelemetry/sdk-trace-node';

const sdk = new NodeSDK({
  traceExporter: new ConsoleSpanExporter(),
  // Configure your exporters (Jaeger, Azure Monitor, etc.)
});

sdk.start();
```

---

# JavaScript Telemetry

This section documents the OpenTelemetry spans and metrics emitted by the JavaScript/TypeScript M365 Agents SDK.

---

## JavaScript Spans

Spans represent individual operations within your agent's request processing. Each span includes attributes that provide context about the operation.

### CloudAdapter Spans

The CloudAdapter is the main entry point for processing incoming activities from channels.

#### agents.adapter.process

Main processing span for incoming activities.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | The type of activity (message, conversationUpdate, etc.) |
| `activity.channel_id` | string | The channel the activity originated from (msteams, webchat, etc.) |
| `activity.delivery_mode` | string | The delivery mode of the activity |
| `activity.conversation_id` | string | The conversation identifier |
| `activity.is_agentic` | boolean | Whether this is an agentic (agent-to-agent) request |

---

#### agents.adapter.send_activities

Span for sending one or more activities to a conversation.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.count` | number | Number of activities being sent |
| `activity.conversation_id` | string | The target conversation identifier |
| `activity.type` | string | The type of each activity (set per activity) |
| `activity.id` | string | The activity identifier (set per activity) |

---

#### agents.adapter.update_activity

Span for updating an existing activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.id` | string | The ID of the activity being updated |
| `activity.conversation_id` | string | The conversation identifier |

---

#### agents.adapter.delete_activity

Span for deleting an activity from a conversation.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.id` | string | The ID of the activity being deleted |
| `activity.conversation_id` | string | The conversation identifier |

---

#### agents.adapter.continue_conversation

Span for continuing a conversation proactively.

| Attribute | Type | Description |
|-----------|------|-------------|
| `bot.app_id` | string | The bot's application ID |
| `activity.conversation_id` | string | The conversation identifier |
| `activity.is_agentic` | boolean | Whether this is an agentic request |

---

#### agents.adapter.create_connector_client

Span for creating a connector client instance.

| Attribute | Type | Description |
|-----------|------|-------------|
| `service_url` | string | The Bot Framework service URL |
| `auth.scope` | string | The authentication scope |
| `activity.is_agentic` | boolean | Whether this is for an agentic request (when using identity variant) |

---

#### agents.adapter.create_user_token_client

Span for creating a user token client instance.

| Attribute | Type | Description |
|-----------|------|-------------|
| `token.service.endpoint` | string | The token service endpoint URL |
| `auth.scope` | string | The authentication scope |

---

### AgentApplication Spans

The AgentApplication class provides a higher-level abstraction for building agents with routing and handlers.

#### agents.app.run

Main execution span for the AgentApplication.

| Attribute | Type | Description |
|-----------|------|-------------|
| `route.authorized` | boolean | Whether the request was authorized |
| `activity.type` | string | The type of activity being processed |
| `activity.id` | string | The activity identifier |
| `route.matched` | boolean | Whether a route handler matched the activity |

**Child Spans:**
- `agents.app.route_handler` - Includes `route.is_invoke` and `route.is_agentic` attributes
- `agents.app.download_files` - Includes `agents.attachments.count` attribute
- `agents.app.before_turn` - Before-turn handlers execution
- `agents.app.after_turn` - After-turn handlers execution

---

#### agents.app.route_handler

Child span for executing a matched route handler.

| Attribute | Type | Description |
|-----------|------|-------------|
| `route.is_invoke` | boolean | Whether this is an invoke activity route |
| `route.is_agentic` | boolean | Whether this is an agentic route |

---

#### agents.app.before_turn

Span for before-turn handlers execution.

*No specific attributes. Wraps the execution of registered before-turn event handlers.*

---

#### agents.app.after_turn

Span for after-turn handlers execution.

*No specific attributes. Wraps the execution of registered after-turn event handlers.*

---

#### agents.app.download_files

Span for downloading file attachments.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agents.attachments.count` | number | Number of attachments being downloaded |

---

### TurnContext Spans

The TurnContext manages the current turn of conversation.

#### agents.turn.send_activities

Span for sending activities through the turn context.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.count` | number | Number of activities being sent |
| `activity.type` | string | The type of each activity (set per activity) |
| `activity.delivery_mode` | string | The delivery mode of each activity (set per activity) |
| `activity.id` | string | The activity identifier (set per activity) |

---

### ConnectorClient Spans

The ConnectorClient handles HTTP communication with the Bot Framework Connector service.

#### agents.connector.reply_to_activity

Span for replying to a specific activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation_id` | string | The conversation identifier |
| `activity.id` | string | The activity being replied to |

---

#### agents.connector.send_to_conversation

Span for sending an activity to a conversation.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation_id` | string | The target conversation identifier |

---

#### agents.connector.update_activity

Span for updating an activity via the connector.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation_id` | string | The conversation identifier |
| `activity.id` | string | The activity being updated |

---

#### agents.connector.delete_activity

Span for deleting an activity via the connector.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation_id` | string | The conversation identifier |
| `activity.id` | string | The activity being deleted |

---

#### agents.connector.create_conversation

Span for creating a new conversation.

*No span-level attributes. Metrics are recorded with operation, http.method, and http.status_code.*

---

#### agents.connector.get_conversations

Span for retrieving conversations.

*No span-level attributes. Metrics are recorded with operation, http.method, and http.status_code.*

---

#### agents.connector.get_conversation_member

Span for retrieving a conversation member.

*No span-level attributes. Metrics are recorded with operation, http.method, and http.status_code.*

---

#### agents.connector.upload_attachment

Span for uploading an attachment.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation_id` | string | The conversation identifier |

---

#### agents.connector.get_attachment_info

Span for retrieving attachment information.

| Attribute | Type | Description |
|-----------|------|-------------|
| `attachment.id` | string | The attachment identifier |

---

#### agents.connector.get_attachment

Span for retrieving attachment content.

| Attribute | Type | Description |
|-----------|------|-------------|
| `attachment.id` | string | The attachment identifier |
| `view.id` | string | The view identifier |

---

### AgentClient Spans

The AgentClient handles agent-to-agent communication.

#### agents.agent_client.post_activity

Span for posting an activity to another agent.

| Attribute | Type | Description |
|-----------|------|-------------|
| `target.endpoint` | string | The target agent's endpoint URL |
| `target.client_id` | string | The target agent's client ID |
| `http.status_code` | string | HTTP response status code |

---

### Storage Spans

Storage spans are emitted by all storage implementations (`MemoryStorage`, `FileStorage`, `BlobsStorage`, `CosmosDbPartitionedStorage`).

#### agents.storage.read

Span for reading items from storage.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.key.count` | number | Number of keys being read |

---

#### agents.storage.write

Span for writing items to storage.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.key.count` | number | Number of keys being written |

---

#### agents.storage.delete

Span for deleting items from storage.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.key.count` | number | Number of keys being deleted |

---

### Authentication Spans

Authentication spans cover token acquisition operations via the MSAL token provider.

#### agents.authentication.get_access_token

Span for acquiring an access token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.scope` | string | The authentication scope requested |
| `auth.method` | string | The authentication method used (secret, certificate, managed_identity, wid, fic) |

---

#### agents.authentication.acquire_token_on_behalf_of

Span for acquiring a token using the on-behalf-of flow.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.scopes` | string[] | The authentication scopes requested |

---

#### agents.authentication.get_agentic_instance_token

Span for acquiring an agentic instance token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agentic.instance_id` | string | The agentic application instance ID |

---

#### agents.authentication.get_agentic_user_token

Span for acquiring an agentic user token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agentic.instance_id` | string | The agentic application instance ID |
| `agentic.user_id` | string | The agentic user ID |
| `auth.scopes` | string[] | The authentication scopes requested |

---

### Authorization Spans

Authorization spans cover the authorization handlers used by AgentApplication.

#### agents.authorization.agentic_token

Span for retrieving an agentic authorization token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used |
| `auth.scopes` | string[] | The authentication scopes requested |

---

#### agents.authorization.azure_bot_token

Span for retrieving an Azure Bot Service authorization token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used |
| `auth.flow` | string | The authentication flow (e.g., "obo") |
| `auth.scopes` | string[] | The authentication scopes (when OBO flow is used) |

---

#### agents.authorization.azure_bot_signin

Span for the Azure Bot Service sign-in flow.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.handler.status` | string | The resulting sign-in status (e.g., APPROVED, REJECTED, REVALIDATE) |
| `auth.handler.status.reason` | string | The reason for the sign-in status |
| `auth.connection.name` | string | The connection name used |

---

#### agents.authorization.azure_bot_signout

Span for signing out a user via Azure Bot Service.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used |
| `activity.channel_id` | string | The channel identifier |

---

### UserTokenClient Spans

The UserTokenClient handles user token operations against the Bot Framework Token Service.

#### agents.user_token_client.get_user_token

Span for getting a user token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |
| `user.id` | string | The user identifier |

---

#### agents.user_token_client.sign_out

Span for signing out a user.

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | The user identifier |
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |

---


#### agents.user_token_client.exchange_token

Span for exchanging a token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | The user identifier |
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_token_or_sign_in_resource

Span for getting a token or sign-in resource.

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | The user identifier |
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_token_status

Span for getting the token status.

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_aad_tokens

Span for getting AAD tokens.

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | The user identifier |
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |

---


## Error Handling in Spans

All spans created by the SDK automatically handle errors. When an exception occurs within a traced operation:

1. The span status is set to `ERROR` with the error message
2. The exception is recorded on the span via `recordException()`
3. A `{spanName}_failed` event is added with `error.type` and `error.message` attributes
4. The span is ended and the error is re-thrown

On success, the span status is set to `OK`.


## Disabling Span Categories

All built-in span categories are enabled by default.

You can selectively disable groups of spans by setting the `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` environment variable. This is useful for reducing telemetry noise in production, where certain span categories (e.g., storage or authentication) may not be needed.

```env
AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES=STORAGE,AUTHORIZATION

# or

AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES=STORAGE AUTHORIZATION
```

Valid category names are:

- `STORAGE`
- `AUTHENTICATION`
- `AUTHORIZATION`

When a span category is disabled, the trace helper still executes your callback with an active non-recording span so your code path and span API calls remain safe, but no telemetry is emitted for that span.


## JavaScript Metrics

Metrics provide aggregated measurements of your agent's operation over time.

### Activity Counters

Counters track the total number of activities processed.

#### agents.activities.received

**Type:** Counter
**Unit:** activities
**Description:** Total number of activities received by the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity received |
| `activity.channel_id` | string | Channel the activity came from |

---

#### agents.activities.sent

**Type:** Counter
**Unit:** activities
**Description:** Total number of outbound activities sent by the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity sent |
| `activity.channel_id` | string | Target channel |

---

#### agents.activities.updated

**Type:** Counter
**Unit:** activities
**Description:** Total number of activities updated by the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel where activity was updated |

---

#### agents.activities.deleted

**Type:** Counter
**Unit:** activities
**Description:** Total number of activities deleted by the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel where activity was deleted |

---

### Request Counters

Counters for outbound requests.

#### agents.connector.request.count

**Type:** Counter
**Unit:** request
**Description:** Total number of outbound connector HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Connector operation name (reply.to.activity, send.to.conversation, etc.) |
| `http.method` | string | HTTP method used (POST, GET, DELETE, PUT) |
| `http.status_code` | string | HTTP response status code |

---

#### agents.agent_client.request.count

**Type:** Counter
**Unit:** request
**Description:** Total number of inter-agent calls.

| Attribute | Type | Description |
|-----------|------|-------------|
| `target.endpoint` | string | Target agent endpoint |
| `http.status_code` | string | HTTP response status code |

---

#### agents.auth.token.request.count

**Type:** Counter
**Unit:** request
**Description:** Total number of token acquisition attempts.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method used (secret, certificate, managed_identity, obo, agentic_instance, agentic_user, etc.) |
| `auth.success` | boolean | Whether the token acquisition succeeded |

---

#### agents.user_token_client.request.count

**Type:** Counter
**Unit:** request
**Description:** Total number of user token client HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Operation name (get.user.token, sign.out, exchange.token, etc.) |
| `http.method` | string | HTTP method used |
| `http.status_code` | string | HTTP response status code |

---

### Turn Counters

Counters for turn processing.

#### agents.turn.count

**Type:** Counter
**Unit:** turn
**Description:** Total turns processed.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity processed |
| `activity.conversation_id` | string | The conversation identifier |

---

#### agents.turn.error.count

**Type:** Counter
**Unit:** turn
**Description:** Total turns that resulted in an error.

| Attribute | Type | Description |
|-----------|------|-------------|
| `error.type` | string | Exception/error class name |

---

### Duration Histograms

Histograms track the distribution of operation durations.

#### agents.adapter.process.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of the adapter process method in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity processed |

---

#### agents.connector.request.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of outbound connector HTTP requests in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Connector operation name |
| `http.status_code` | string | HTTP response status code |

---

#### agents.agent_client.request.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of inter-agent call latency in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `target.endpoint` | string | Target agent endpoint |

---

#### agents.turn.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of end-to-end turn processing in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity processed |
| `activity.channel_id` | string | The channel identifier |
| `activity.conversation_id` | string | The conversation identifier |

---

#### agents.storage.operation.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of storage operations in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.operation` | string | Storage operation type (read, write, delete) |

---

#### agents.auth.token.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of token acquisition latency in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method used |

---

#### agents.user_token_client.request.duration

**Type:** Histogram
**Unit:** ms
**Description:** Duration of user token client HTTP requests in milliseconds.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Operation name |
| `http.status_code` | string | HTTP response status code |

---

## JavaScript Span Constants Reference

All span names are available as constants in the `@microsoft/agents-telemetry` package:

```typescript
import { SpanNames } from '@microsoft/agents-telemetry';

// CloudAdapter
SpanNames.ADAPTER_PROCESS                  // 'agents.adapter.process'
SpanNames.ADAPTER_SEND_ACTIVITIES          // 'agents.adapter.send_activities'
SpanNames.ADAPTER_UPDATE_ACTIVITY          // 'agents.adapter.update_activity'
SpanNames.ADAPTER_DELETE_ACTIVITY          // 'agents.adapter.delete_activity'
SpanNames.ADAPTER_CONTINUE_CONVERSATION    // 'agents.adapter.continue_conversation'
SpanNames.ADAPTER_CREATE_CONNECTOR_CLIENT  // 'agents.adapter.create_connector_client'
SpanNames.ADAPTER_CREATE_USER_TOKEN_CLIENT // 'agents.adapter.create_user_token_client'

// AgentApplication
SpanNames.AGENTS_APP_RUN               // 'agents.app.run'
SpanNames.AGENTS_APP_ROUTE_HANDLER     // 'agents.app.route_handler'
SpanNames.AGENTS_APP_BEFORE_TURN       // 'agents.app.before_turn'
SpanNames.AGENTS_APP_AFTER_TURN        // 'agents.app.after_turn'
SpanNames.AGENTS_APP_DOWNLOAD_FILES    // 'agents.app.download_files'

// ConnectorClient
SpanNames.CONNECTOR_SEND_TO_CONVERSATION   // 'agents.connector.send_to_conversation'
SpanNames.CONNECTOR_REPLY_TO_ACTIVITY      // 'agents.connector.reply_to_activity'
SpanNames.CONNECTOR_UPDATE_ACTIVITY        // 'agents.connector.update_activity'
SpanNames.CONNECTOR_DELETE_ACTIVITY        // 'agents.connector.delete_activity'
SpanNames.CONNECTOR_CREATE_CONVERSATION    // 'agents.connector.create_conversation'
SpanNames.CONNECTOR_GET_CONVERSATIONS      // 'agents.connector.get_conversations'
SpanNames.CONNECTOR_GET_CONVERSATION_MEMBER // 'agents.connector.get_conversation_member'
SpanNames.CONNECTOR_UPLOAD_ATTACHMENT      // 'agents.connector.upload_attachment'
SpanNames.CONNECTOR_GET_ATTACHMENT         // 'agents.connector.get_attachment'
SpanNames.CONNECTOR_GET_ATTACHMENT_INFO    // 'agents.connector.get_attachment_info'

// Storage
SpanNames.STORAGE_READ                 // 'agents.storage.read'
SpanNames.STORAGE_WRITE                // 'agents.storage.write'
SpanNames.STORAGE_DELETE               // 'agents.storage.delete'

// AgentClient
SpanNames.AGENT_CLIENT_POST_ACTIVITY   // 'agents.agent_client.post_activity'

// Authentication
SpanNames.AUTHENTICATION_GET_ACCESS_TOKEN            // 'agents.authentication.get_access_token'
SpanNames.AUTHENTICATION_ACQUIRE_TOKEN_ON_BEHALF_OF  // 'agents.authentication.acquire_token_on_behalf_of'
SpanNames.AUTHENTICATION_GET_AGENTIC_INSTANCE_TOKEN  // 'agents.authentication.get_agentic_instance_token'
SpanNames.AUTHENTICATION_GET_AGENTIC_USER_TOKEN      // 'agents.authentication.get_agentic_user_token'

// Authorization
SpanNames.AUTHORIZATION_AGENTIC_TOKEN      // 'agents.authorization.agentic_token'
SpanNames.AUTHORIZATION_AZURE_BOT_TOKEN    // 'agents.authorization.azure_bot_token'
SpanNames.AUTHORIZATION_AZURE_BOT_SIGNIN   // 'agents.authorization.azure_bot_signin'
SpanNames.AUTHORIZATION_AZURE_BOT_SIGNOUT  // 'agents.authorization.azure_bot_signout'

// UserTokenClient
SpanNames.USER_TOKEN_CLIENT_GET_USER_TOKEN              // 'agents.user_token_client.get_user_token'
SpanNames.USER_TOKEN_CLIENT_SIGN_OUT                    // 'agents.user_token_client.sign_out'
SpanNames.USER_TOKEN_CLIENT_EXCHANGE_TOKEN               // 'agents.user_token_client.exchange_token'
SpanNames.USER_TOKEN_CLIENT_GET_TOKEN_OR_SIGN_IN_RESOURCE // 'agents.user_token_client.get_token_or_sign_in_resource'
SpanNames.USER_TOKEN_CLIENT_GET_TOKEN_STATUS             // 'agents.user_token_client.get_token_status'
SpanNames.USER_TOKEN_CLIENT_GET_AAD_TOKENS               // 'agents.user_token_client.get_aad_tokens'

// TurnContext
SpanNames.TURN_SEND_ACTIVITIES         // 'agents.turn.send_activities'
```

---

## JavaScript Metric Constants Reference

All metric names are available as constants:

```typescript
import { MetricNames } from '@microsoft/agents-telemetry';

// CloudAdapter
MetricNames.ADAPTER_PROCESS_DURATION      // 'agents.adapter.process.duration'

// Activity counters
MetricNames.ACTIVITIES_RECEIVED           // 'agents.activities.received'
MetricNames.ACTIVITIES_SENT               // 'agents.activities.sent'
MetricNames.ACTIVITIES_UPDATED            // 'agents.activities.updated'
MetricNames.ACTIVITIES_DELETED            // 'agents.activities.deleted'

// Connector metrics
MetricNames.CONNECTOR_REQUESTS            // 'agents.connector.request.count'
MetricNames.CONNECTOR_REQUEST_DURATION    // 'agents.connector.request.duration'

// AgentClient metrics
MetricNames.AGENT_CLIENT_REQUESTS         // 'agents.agent_client.request.count'
MetricNames.AGENT_CLIENT_REQUEST_DURATION // 'agents.agent_client.request.duration'

// Turn metrics
MetricNames.TURNS_COUNT                   // 'agents.turn.count'
MetricNames.TURNS_ERRORS                  // 'agents.turn.error.count'
MetricNames.TURN_DURATION                 // 'agents.turn.duration'

// Storage metrics
MetricNames.STORAGE_OPERATION_DURATION    // 'agents.storage.operation.duration'

// Authentication metrics
MetricNames.AUTH_TOKEN_REQUEST_COUNT    // 'agents.auth.token.request.count'
MetricNames.AUTH_TOKEN_DURATION           // 'agents.auth.token.duration'

// UserTokenClient metrics
MetricNames.USER_TOKEN_CLIENT_REQUESTS          // 'agents.user_token_client.request.count'
MetricNames.USER_TOKEN_CLIENT_REQUEST_DURATION  // 'agents.user_token_client.request.duration'
```

---

# C# Telemetry

This section documents the OpenTelemetry spans and metrics emitted by the C# M365 Agents SDK.

---

## System.Diagnostics Integration

The C# SDK has **no dependency on OpenTelemetry**. All instrumentation is built on top of `System.Diagnostics`, the distributed-tracing and metrics layer built into the .NET runtime:

- Spans are `System.Diagnostics.Activity` objects created from a shared `ActivitySource`.
- Metrics are `Counter<T>` and `Histogram<T>` instruments created from a shared `Meter`.

Both are published under the source name **`Microsoft.Agents.Core`**. Any listener subscribed to that name receives the telemetry — OpenTelemetry is just one possible listener.

```csharp
// The single source used for all SDK traces and metrics
AgentsTelemetry.SourceName    // "Microsoft.Agents.Core"
AgentsTelemetry.ActivitySource // System.Diagnostics.ActivitySource
AgentsTelemetry.Meter          // System.Diagnostics.Metrics.Meter
```

This design means the SDK integrates naturally with any observability tool that understands `System.Diagnostics`, not only with the OpenTelemetry SDK.

---

## C# Setup

Add the OpenTelemetry NuGet packages to your project:

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />
```

Then subscribe to the `Microsoft.Agents.Core` source and meter in `Program.cs`:

```csharp
using Microsoft.Agents.Core.Telemetry;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: AgentsTelemetry.SourceName,
        serviceVersion: AgentsTelemetry.SourceVersion))
    .WithTracing(tracing => tracing
        .AddSource(AgentsTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AgentsTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

Because the SDK emits standard `System.Diagnostics` signals, you can substitute any exporter (Console, Azure Monitor, Jaeger, Prometheus, etc.) without changing the agent code.

For a complete working example including console and OTLP exporters, see the [`TelemetryAgent` sample](src/samples/InstrumentedAgent/).

---

## C# Spans

In the C# SDK, what this document calls a "span" is a `System.Diagnostics.Activity` created from `AgentsTelemetry.ActivitySource`. The term "span" is used here for consistency with the JavaScript and Python SDKs, whose observability is built directly on OpenTelemetry. The underlying `System.Diagnostics.Activity` objects are fully compatible with OpenTelemetry when a listener such as the OpenTelemetry .NET SDK is configured.

---

### C# CloudAdapter Spans

#### agents.adapter.process

Main processing span for each incoming activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | The type of activity (message, conversationUpdate, etc.) |
| `activity.channel_id` | string | The channel the activity originated from |
| `activity.delivery_mode` | string | The delivery mode of the activity |
| `activity.conversation.id` | string | The conversation identifier |
| `activity.is_agentic_request` | boolean | Whether this is an agent-to-agent request |

---

#### agents.adapter.write_response

Span for synchronous responses: `invoke` activities and `expectReplies` delivery mode.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activities.count` | integer | Number of activities in the response |
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.adapter.send_activities

Span for sending one or more activities through the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activities.count` | integer | Number of activities being sent |
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.adapter.update_activity

Span for updating an existing activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.id` | string | The ID of the activity being updated |
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.adapter.delete_activity

Span for deleting an activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | The type of the activity being deleted |
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.adapter.continue_conversation

Span for continuing a conversation proactively.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agent.app_id` | string | The agent's application ID |
| `activity.conversation.id` | string | The conversation identifier |
| `activity.is_agentic_request` | boolean | Whether this is an agentic request |

---

#### agents.adapter.create_connector_client

Span for creating a connector client instance.

| Attribute | Type | Description |
|-----------|------|-------------|
| `service_url` | string | The Bot Framework service URL |
| `auth.scopes` | string | Comma-separated authentication scopes |
| `activity.is_agentic_request` | boolean | Whether this is for an agentic request |

---

#### agents.adapter.create_user_token_client

Span for creating a user token client instance.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agents.token_service.endpoint` | string | The token service endpoint URL |

---

### C# AgentApplication Spans

#### agents.app.run

Main execution span for a turn through `AgentApplication`.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | The type of activity being processed |
| `activity.channel_id` | string | The channel identifier |
| `activity.conversation.id` | string | The conversation identifier |
| `activity.id` | string | The activity identifier |
| `route.authorized` | boolean | Whether the request was authorized |
| `route.matched` | boolean | Whether a route handler matched the activity |

---

#### agents.app.route_handler

Child span for executing a matched route handler.

| Attribute | Type | Description |
|-----------|------|-------------|
| `route.is_invoke` | boolean | Whether this is an invoke activity route |
| `route.is_agentic` | boolean | Whether this is an agentic route |

---

#### agents.app.before_turn

Span for before-turn handler execution. No additional attributes.

---

#### agents.app.after_turn

Span for after-turn handler execution. No additional attributes.

---

#### agents.app.download_files

Span for downloading file attachments.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachments.count` | integer | Number of attachments on the activity |

---

### C# TurnContext Spans

#### agents.turn.send_activities

Span emitted by `TurnContext` each time activities are sent.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The conversation identifier |

---

### C# ConnectorClient Spans

#### agents.connector.reply_to_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The conversation identifier |
| `activity.id` | string | The activity being replied to |

---

#### agents.connector.send_to_conversation

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The target conversation identifier |

---

#### agents.connector.update_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The conversation identifier |
| `activity.id` | string | The activity being updated |

---

#### agents.connector.delete_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.connector.create_conversation

No span-level attributes.

---

#### agents.connector.get_conversations

No span-level attributes.

---

#### agents.connector.get_conversation_members

No span-level attributes.

---

#### agents.connector.upload_attachment

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | The conversation identifier |

---

#### agents.connector.get_attachment_info

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachment.id` | string | The attachment identifier |

---

#### agents.connector.get_attachment

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachment.id` | string | The attachment identifier |
| `view.id` | string | The view identifier |

---

### C# UserTokenClient Spans

All user token client spans record `auth.connection.name`, `user.id`, and `activity.channel_id` when provided.

#### agents.user_token_client.get_user_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.sign_out

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_sign_in_resource

*C#-only span — not present in the JS or Python SDKs.*

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.exchange_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_token_or_sign_in_resource

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_token_status

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.user_token_client.get_aad_tokens

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The OAuth connection name |
| `user.id` | string | The user identifier |
| `activity.channel_id` | string | The channel identifier |

---

### C# Storage Spans

All storage implementations (`MemoryStorage`, `BlobsStorage`, `CosmosDbPartitionedStorage`) emit these spans.

#### agents.storage.read

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being read |
| `storage.operation` | string | Always `"read"` |

---

#### agents.storage.write

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being written |
| `storage.operation` | string | Always `"write"` |

---

#### agents.storage.delete

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being deleted |
| `storage.operation` | string | Always `"delete"` |

---

### C# Authentication Spans

#### agents.authentication.get_access_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | The authentication method (e.g., `"secret"`, `"certificate"`, `"managed_identity"`) |
| `auth.scopes` | string | Comma-separated authentication scopes |

---

#### agents.authentication.acquire_token_on_behalf_of

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Always `"obo"` |
| `auth.scopes` | string | Comma-separated authentication scopes |

---

#### agents.authentication.get_agentic_instance_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Always `"agentic_instance"` |
| `agentic.instance_id` | string | The agentic application instance ID |

---

#### agents.authentication.get_agentic_user_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Always `"agentic_user"` |
| `agentic.instance_id` | string | The agentic application instance ID |
| `agentic.user_id` | string | The agentic user ID |
| `auth.scopes` | string | Comma-separated authentication scopes |

---

### C# Authorization Spans

#### agents.authorization.agentic_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name (omitted if not provided) |
| `auth.scopes` | string | Comma-separated scopes (omitted if not provided) |

---

#### agents.authorization.azure_bot_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name (omitted if not provided) |
| `auth.scopes` | string | Comma-separated scopes (omitted if not provided) |

---

#### agents.authorization.azure_bot_signin

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name (omitted if not provided) |
| `auth.scopes` | string | Comma-separated scopes (omitted if not provided) |
| `activity.channel_id` | string | The channel identifier |

---

#### agents.authorization.azure_bot_signout

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name |
| `activity.channel_id` | string | The channel identifier |

---

## C# Error Handling in Spans

All spans created by the SDK automatically handle errors. When an exception propagates out of a traced operation:

1. The span status is set to `Error` with the exception message.
2. An `exception` event is added to the `System.Diagnostics.Activity` with `exception.type`, `exception.message`, and `exception.stacktrace` tags, following OpenTelemetry semantic conventions.
3. The exception is re-raised — it is never swallowed by the telemetry layer.

On clean exit the activity's status is left at the default (`Unset`), which OpenTelemetry exporters render as `OK`.

---

## C# Metrics

All metric instruments are created from `AgentsTelemetry.Meter` (name `"Microsoft.Agents.Core"`).

### C# Activity Counters

#### agents.activities.received

**Type:** Counter  
**Unit:** activity  
**Description:** Total activities received by the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.sent

**Type:** Counter  
**Unit:** activity  
**Description:** Total outbound activities sent.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.updated

**Type:** Counter  
**Unit:** activity  
**Description:** Total activity updates.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.deleted

**Type:** Counter  
**Unit:** activity  
**Description:** Total activity deletions.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel identifier |

---

### C# Turn Metrics

#### agents.turn.count

**Type:** Counter  
**Unit:** turn  
**Description:** Total turns successfully processed.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.turn.error.count

**Type:** Counter  
**Unit:** turn  
**Description:** Total turns that resulted in an error.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.turn.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** End-to-end turn processing duration. Recorded only on successful turns — not recorded when the turn raises an exception.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

### C# Adapter Metrics

#### agents.adapter.process.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of the adapter process method.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

### C# Connector Metrics

#### agents.connector.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total outbound connector HTTP requests.

*No metric-level attributes.*

---

#### agents.connector.request.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of outbound connector HTTP requests.

*No metric-level attributes.*

---

### C# UserTokenClient Metrics

#### agents.user_token_client.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total outbound user token client HTTP requests.

*No metric-level attributes.*

---

#### agents.user_token_client.request.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of outbound user token client HTTP requests.

*No metric-level attributes.*

---

### C# Storage Metrics

#### agents.storage.operation.total

**Type:** Counter  
**Unit:** operation  
**Description:** Total storage operations performed.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.operation` | string | `"read"`, `"write"`, or `"delete"` |

---

#### agents.storage.operation.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of storage operations.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.operation` | string | `"read"`, `"write"`, or `"delete"` |

---

### C# Authentication Metrics

#### agents.auth.token.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total token requests made to the authentication service.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method (e.g., `"secret"`, `"obo"`, `"agentic_instance"`, `"agentic_user"`) |

---

#### agents.auth.token.request.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of token requests to the authentication service.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method |

---

## C# Span Constants Reference

Span name constants are defined across several `Constants` classes in the SDK:

> **Note:** Most of these constants are currently `internal`. They are listed here as string literals for reference. We are considering making them public once the telemetry implementation is more stable.

```csharp
using Microsoft.Agents.Builder.Telemetry.Adapter;
using Microsoft.Agents.Builder.Telemetry.App;
using Microsoft.Agents.Builder.Telemetry.TurnContext;
using Microsoft.Agents.Builder.Telemetry.Authorization;
using Microsoft.Agents.Connector.Telemetry;
using Microsoft.Agents.Storage.Telemetry;
using Microsoft.Agents.Authentication.Telemetry;

// CloudAdapter
"agents.adapter.process"
"agents.adapter.write_response"
"agents.adapter.send_activities"
"agents.adapter.update_activity"
"agents.adapter.delete_activity"
"agents.adapter.continue_conversation"
"agents.adapter.create_connector_client"
"agents.adapter.create_user_token_client"

// AgentApplication
"agents.app.run"
"agents.app.route_handler"
"agents.app.before_turn"
"agents.app.after_turn"
"agents.app.download_files"

// TurnContext
"agents.turn.send_activities"

// ConnectorClient
"agents.connector.reply_to_activity"
"agents.connector.send_to_conversation"
"agents.connector.update_activity"
"agents.connector.delete_activity"
"agents.connector.create_conversation"
"agents.connector.get_conversations"
"agents.connector.get_conversation_members"
"agents.connector.upload_attachment"
"agents.connector.get_attachment_info"
"agents.connector.get_attachment"

// UserTokenClient
"agents.user_token_client.get_user_token"
"agents.user_token_client.sign_out"
"agents.user_token_client.get_sign_in_resource"   // C#-only
"agents.user_token_client.exchange_token"
"agents.user_token_client.get_token_or_sign_in_resource"
"agents.user_token_client.get_token_status"
"agents.user_token_client.get_aad_tokens"

// Storage
"agents.storage.read"
"agents.storage.write"
"agents.storage.delete"

// Authentication
"agents.authentication.get_access_token"
"agents.authentication.acquire_token_on_behalf_of"
"agents.authentication.get_agentic_instance_token"
"agents.authentication.get_agentic_user_token"

// Authorization
"agents.authorization.agentic_token"
"agents.authorization.azure_bot_token"
"agents.authorization.azure_bot_signin"
"agents.authorization.azure_bot_signout"
```

---

## C# Attribute Constants Reference

All attribute key strings are defined in `Microsoft.Agents.Core.Telemetry.TagNames`:

```csharp
using Microsoft.Agents.Core.Telemetry;

TagNames.ActivityDeliveryMode   // "activity.delivery_mode"
TagNames.ActivityChannelId      // "activity.channel_id"
TagNames.ActivityId             // "activity.id"
TagNames.ActivityCount          // "activities.count"
TagNames.ActivityType           // "activity.type"
TagNames.AgenticUserId          // "agentic.user_id"
TagNames.AgenticInstanceId      // "agentic.instance_id"
TagNames.AppId                  // "agent.app_id"
TagNames.AttachmentId           // "activity.attachment.id"
TagNames.AttachmentCount        // "activity.attachments.count"
TagNames.AuthHandlerId          // "auth.handler.id"
TagNames.AuthMethod             // "auth.method"
TagNames.AuthScopes             // "auth.scopes"
TagNames.AuthSuccess            // "auth.success"
TagNames.ConversationId         // "activity.conversation.id"
TagNames.ExchangeConnection     // "auth.connection.name"
TagNames.HttpMethod             // "http.method"
TagNames.HttpStatusCode         // "http.status_code"
TagNames.IsAgentic              // "activity.is_agentic_request"
TagNames.KeyCount               // "storage.keys.count"
TagNames.Operation              // "operation"
TagNames.RouteAuthorized        // "route.authorized"
TagNames.RouteIsInvoke          // "route.is_invoke"
TagNames.RouteIsAgentic         // "route.is_agentic"
TagNames.RouteMatched           // "route.matched"
TagNames.ServiceUrl             // "service_url"
TagNames.StorageOperation       // "storage.operation"
TagNames.TokenServiceEndpoint   // "agents.token_service.endpoint"
TagNames.UserId                 // "user.id"
TagNames.ViewId                 // "view.id"
```

---

# Python Telemetry

This section documents the OpenTelemetry spans and metrics emitted by the Python M365 Agents SDK (`microsoft-agents-hosting-core`).

> **Note on attribute naming**: The Python SDK uses a slightly different set of attribute key strings than the JavaScript SDK in several places. Differences are noted inline in each span and metric table.

---

## Python Setup

Install the required packages:

```bash
pip install opentelemetry-sdk opentelemetry-api
# For exporting to an OTLP collector (Jaeger, Grafana, Azure Monitor, etc.)
pip install opentelemetry-exporter-otlp
```

Configure providers **before** your agent starts processing requests:

```python
from opentelemetry import metrics, trace
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter

endpoint = "http://localhost:4317/"  # your OTLP collector

resource = Resource.create({
    "service.name": "my-agent",
    "service.version": "1.0.0",
})

# Tracing
tracer_provider = TracerProvider(resource=resource)
tracer_provider.add_span_processor(SimpleSpanProcessor(OTLPSpanExporter(endpoint=endpoint)))
trace.set_tracer_provider(tracer_provider)

# Metrics
meter_provider = MeterProvider(
    resource=resource,
    metric_readers=[PeriodicExportingMetricReader(OTLPMetricExporter(endpoint=endpoint))],
)
metrics.set_meter_provider(meter_provider)
```

> **Important**: Providers must be configured before most SDK modules are imported. The tracer and meter are resolved at import time, so OpenTelemetry must be fully set up before importing packages such as `microsoft_agents.hosting.core`. `microsoft_agents.activity` contains no telemetry and is safe to import beforehand.

The SDK can also instrument the `aiohttp` server, `aiohttp` client, and `requests` libraries via optional OpenTelemetry instrumentation packages:

```bash
pip install opentelemetry-instrumentation-aiohttp-server
pip install opentelemetry-instrumentation-aiohttp-client
pip install opentelemetry-instrumentation-requests
```

See `test_samples/otel/src/telemetry.py` for a complete example using `AioHttpServerInstrumentor`, `AioHttpClientInstrumentor`, and `RequestsInstrumentor`.

---

## Python Spans

### Python Adapter Spans

The `HttpAdapterBase` (and its subclasses in `aiohttp`/`FastAPI` hosting packages) emit spans for every incoming request and outgoing activity.

#### agents.adapter.process

Main processing span. Entered before the HTTP method check, so it fires for every call to `process_request`.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type (message, invoke, etc.) — set only after successful JSON parse |
| `activity.channel_id` | string | Channel identifier — set only after successful JSON parse |
| `activity.delivery_mode` | string | Delivery mode of the activity |
| `activity.conversation.id` | string | Conversation identifier |
| `activity.is_agentic_request` | boolean | Whether this is an agent-to-agent request |

> **Note**: Attributes are set only after successful JSON parsing. If parsing fails or the request is not a POST, activity type and channel default to `"unknown"` in metrics.

---

#### agents.adapter.write_response

Emitted for activities that require a synchronous response: `invoke` activities and `expectReplies` delivery mode.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |

---

#### agents.adapter.send_activities

Span for sending one or more activities through the adapter.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activities.count` | integer | Number of activities being sent |
| `activity.conversation.id` | string | Conversation identifier |

---

#### agents.adapter.update_activity

Span for updating an activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.id` | string | ID of the activity being updated |
| `activity.conversation.id` | string | Conversation identifier |

---

#### agents.adapter.delete_activity

Span for deleting an activity.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.id` | string | ID of the activity being deleted |
| `activity.conversation.id` | string | Conversation identifier |

---

#### agents.adapter.continue_conversation

Span for proactively continuing a conversation.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agent.app_id` | string | The bot's application ID |
| `activity.conversation.id` | string | Conversation identifier |
| `activity.is_agentic_request` | boolean | Whether this is an agentic request |

---

#### agents.adapter.create_connector_client

Span for creating a connector client.

| Attribute | Type | Description |
|-----------|------|-------------|
| `service_url` | string | Bot Framework service URL |
| `auth.scopes` | string | Comma-separated authentication scopes |
| `activity.is_agentic_request` | boolean | Whether this is for an agentic request |

---

#### agents.adapter.create_user_token_client

Span for creating a user token client.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agents.token_service.endpoint` | string | Token service endpoint URL |
| `auth.scopes` | string | Comma-separated authentication scopes |

---

### Python AgentApplication Spans

`AgentApplication` (the decorator-based programming model) emits spans for turn lifecycle events.

#### agents.app.run

Main execution span for an `AgentApplication` turn.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Type of activity being processed |
| `activity.id` | string | Activity identifier (`"unknown"` if not set) |
| `route.authorized` | boolean | Whether the route was authorized |
| `route.matched` | boolean | Whether a route handler matched the activity |

---

#### agents.app.route_handler

Child span for executing a matched route handler.

| Attribute | Type | Description |
|-----------|------|-------------|
| `route.is_invoke` | boolean | Whether this is an invoke activity route |
| `route.is_agentic` | boolean | Whether this is an agentic route |

---

#### agents.app.before_turn

Span for before-turn handlers execution. No additional attributes.

---

#### agents.app.after_turn

Span for after-turn handlers execution. No additional attributes.

---

#### agents.app.download_files

Span for downloading file attachments.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachments.count` | integer | Number of attachments (0 when `activity.attachments` is None) |

---

### Python TurnContext Spans

#### agents.turn.send_activities

Emitted by `TurnContext` each time an activity is sent.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |

---

### Python ConnectorClient Spans

`ConnectorClient` wraps each outbound HTTP call in a span. HTTP method and status code appear only in metrics, not on the span itself.

#### agents.connector.reply_to_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |
| `activity.id` | string | The activity being replied to |

---

#### agents.connector.send_to_conversation

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Target conversation identifier |

---

#### agents.connector.update_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |
| `activity.id` | string | The activity being updated |

---

#### agents.connector.delete_activity

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |
| `activity.id` | string | The activity being deleted |

---

#### agents.connector.create_conversation

No span-level attributes. Metrics are recorded with `operation`, `http.method`, and `http.status_code`.

---

#### agents.connector.get_conversations

No span-level attributes. Metrics are recorded with `operation`, `http.method`, and `http.status_code`.

---

#### agents.connector.get_conversation_members

No span-level attributes. Metrics are recorded with `operation`, `http.method`, and `http.status_code`.

---

#### agents.connector.upload_attachment

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.conversation.id` | string | Conversation identifier |

---

#### agents.connector.get_attachment_info

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachment.id` | string | Attachment identifier |

---

#### agents.connector.get_attachment

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.attachment.id` | string | Attachment identifier |
| `view.id` | string | View identifier |

---

### Python Storage Spans

All storage implementations (`MemoryStorage`, `BlobStorage`, `CosmosDbStorage`) emit these spans.

#### agents.storage.read

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being read |
| `storage.operation` | string | Always `"read"` |

---

#### agents.storage.write

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being written |
| `storage.operation` | string | Always `"write"` |

---

#### agents.storage.delete

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.keys.count` | integer | Number of keys being deleted |
| `storage.operation` | string | Always `"delete"` |

---

### Python UserTokenClient Spans

HTTP method and status code appear only in metrics, not on the span itself.

#### agents.user_token_client.get_user_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | OAuth connection name |
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |

---

#### agents.user_token_client.sign_out

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | OAuth connection name (`"unknown"` if None) |
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |

---

#### agents.user_token_client.exchange_token

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | OAuth connection name |
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |

---

#### agents.user_token_client.get_token_or_sign_in_resource

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | OAuth connection name |
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |

---

#### agents.user_token_client.get_token_status

| Attribute | Type | Description |
|-----------|------|-------------|
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |
| `auth.connection.name` | string | Always `"unknown"` |

---

#### agents.user_token_client.get_aad_tokens

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | OAuth connection name |
| `user.id` | string | User identifier |
| `activity.channel_id` | string | Channel identifier (`"unknown"` if not provided) |

---

### Python Authentication Spans

MSAL token acquisition operations emit these spans. Each authentication span also records [Authentication Metrics](#python-auth-metrics).

#### agents.authentication.get_access_token

Span for acquiring an access token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.scopes` | string | Comma-separated authentication scopes |
| `auth.method` | string | Authentication method used (e.g., `"secret"`, `"certificate"`, `"managed_identity"`) |

---

#### agents.authentication.acquire_token_on_behalf_of

Span for acquiring a token using the on-behalf-of flow.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.scopes` | string | Comma-separated authentication scopes |

---

#### agents.authentication.get_agentic_instance_token

Span for acquiring an agentic instance token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agentic.instance_id` | string | The agentic application instance ID |

---

#### agents.authentication.get_agentic_user_token

Span for acquiring an agentic user token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `agentic.instance_id` | string | The agentic application instance ID |
| `agentic.user_id` | string | The agentic user ID |
| `auth.scopes` | string | Comma-separated authentication scopes |

---

### Python Authorization Spans

OAuth sign-in and token operations emit these spans.

#### agents.authorization.agentic_token

Span for retrieving an agentic authorization token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used (`"unknown"` if not provided) |
| `auth.scopes` | string | Comma-separated authentication scopes (omitted if no scopes provided) |

---

#### agents.authorization.azure_bot_token

Span for retrieving an Azure Bot Service authorization token.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used (`"unknown"` if not provided) |
| `auth.scopes` | string | Comma-separated authentication scopes (omitted if no scopes provided) |

---

#### agents.authorization.azure_bot_signin

Span for the Azure Bot Service sign-in flow.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used (`"unknown"` if not provided) |
| `auth.scopes` | string | Comma-separated authentication scopes (omitted if no scopes provided) |

---

#### agents.authorization.azure_bot_signout

Span for signing out a user via Azure Bot Service.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name (`"unknown"` — no connection name applies to sign-out) |

---

## Python Metrics

### Python Adapter Metrics

#### agents.adapter.process.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of each `process_request` call.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type (`"unknown"` if JSON parsing failed) |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.received

**Type:** Counter  
**Unit:** activity  
**Description:** Total activities received — incremented once per `process_request` call, including non-POST and bad-JSON calls (where type and channel default to `"unknown"`).

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.sent

**Type:** Counter  
**Unit:** activity  
**Description:** Total outbound activities sent.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.updated

**Type:** Counter  
**Unit:** activity  
**Description:** Total activity updates.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel identifier |

---

#### agents.activities.deleted

**Type:** Counter  
**Unit:** activity  
**Description:** Total activity deletions.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.channel_id` | string | Channel identifier |

---

### Python Turn Metrics

#### agents.turn.count

**Type:** Counter  
**Unit:** turn  
**Description:** Total successful turns (no exception).

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |
| `activity.conversation.id` | string | Conversation identifier |
| `success` | boolean | Always `True` for this metric |

---

#### agents.turn.error.count

**Type:** Counter  
**Unit:** turn  
**Description:** Total turns that raised an exception.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |
| `activity.conversation.id` | string | Conversation identifier |
| `success` | boolean | Always `False` for this metric |

---

#### agents.turn.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** End-to-end turn duration. Recorded on both success and error paths.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Activity type |
| `activity.channel_id` | string | Channel identifier |
| `activity.conversation.id` | string | Conversation identifier |
| `success` | boolean | Whether the turn completed without error |

---

### Python Storage Metrics

#### agents.storage.operation.total

**Type:** Counter  
**Unit:** operation  
**Description:** Total storage operations (Python-only — no equivalent in the JavaScript SDK).

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.operation` | string | `"read"`, `"write"`, or `"delete"` |

---

#### agents.storage.operation.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of storage operations.

| Attribute | Type | Description |
|-----------|------|-------------|
| `storage.operation` | string | `"read"`, `"write"`, or `"delete"` |

---

### Python Connector Metrics

#### agents.connector.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total outbound connector HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Span name of the operation (e.g. `"agents.connector.reply_to_activity"`) |
| `http.method` | string | HTTP method — present only when the HTTP response is available |
| `http.status_code` | integer | HTTP status code — present only when the HTTP response is available |

---

#### agents.connector.request.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of outbound connector HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Span name of the operation |
| `http.method` | string | HTTP method — present only when the HTTP response is available |
| `http.status_code` | integer | HTTP status code — present only when the HTTP response is available |

---

### Python UserTokenClient Metrics

#### agents.user_token_client.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total user token client HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Span name of the operation (e.g. `"agents.user_token_client.get_user_token"`) |
| `http.method` | string | HTTP method — present only when the HTTP response is available |
| `http.status_code` | integer | HTTP status code — present only when the HTTP response is available |

---

#### agents.user_token_client.request.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of user token client HTTP requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `operation` | string | Span name of the operation |
| `http.method` | string | HTTP method — present only when the HTTP response is available |
| `http.status_code` | integer | HTTP status code — present only when the HTTP response is available |

---

### Python Authentication Metrics

#### agents.auth.token.request.count

**Type:** Counter  
**Unit:** request  
**Description:** Total authentication token requests made by the SDK.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method used (e.g., `"secret"`, `"certificate"`, `"obo"`, `"agentic_instance"`, `"agentic_user"`) |
| `auth.success` | boolean | Whether the token request succeeded |

---

#### agents.auth.token.duration

**Type:** Histogram  
**Unit:** ms  
**Description:** Duration of authentication token requests.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.method` | string | Authentication method used |
| `auth.success` | boolean | Whether the token request succeeded |

---

## Python Disabling Span Categories

To suppress specific spans in production, use an OpenTelemetry [sampler](https://opentelemetry.io/docs/concepts/sampling/). A sampler receives the span name before any work is done and can return `DROP` to discard it entirely — no attributes are set, no metrics are recorded for that span.

```python
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.sampling import ParentBased, Decision, Sampler, SamplingResult

SUPPRESSED_SPANS = {
    "agents.storage.read",
    "agents.storage.write",
    "agents.storage.delete",
}

class SpanFilterSampler(Sampler):
    def should_sample(self, parent_context, trace_id, name, kind, attributes, links):
        if name in SUPPRESSED_SPANS:
            return SamplingResult(Decision.DROP)
        return SamplingResult(Decision.RECORD_AND_SAMPLE)

    def get_description(self):
        return "SpanFilterSampler"

tracer_provider = TracerProvider(sampler=ParentBased(root=SpanFilterSampler()))
trace.set_tracer_provider(tracer_provider)
```

Use the span name constants from the [Span Constants Reference](#python-span-constants-reference) to identify which spans to suppress.

---

## Python Error Handling in Spans

All SDK spans automatically handle errors. When an exception propagates out of a traced operation:

1. The span status is set to `ERROR`.
2. The exception is recorded on the span via `span.record_exception()`.
3. The exception is re-raised — it is never swallowed by the telemetry layer.

On clean exit, the span status is set to `OK`.

---

## Python Span Constants Reference

Span name constants are defined across several modules in `microsoft_agents.hosting.core`:

```python
from microsoft_agents.hosting.core.telemetry.adapter import constants as adapter_constants
from microsoft_agents.hosting.core.app.telemetry import constants as app_constants
from microsoft_agents.hosting.core.connector.telemetry import constants as connector_constants
from microsoft_agents.hosting.core.storage.telemetry import constants as storage_constants
from microsoft_agents.hosting.core.telemetry.turn_context import constants as turn_context_constants
from microsoft_agents.hosting.core.authorization.telemetry import constants as auth_constants
from microsoft_agents.hosting.core.app.oauth.telemetry import constants as oauth_constants

# Adapter
adapter_constants.SPAN_PROCESS                    # 'agents.adapter.process'
adapter_constants.SPAN_SEND_ACTIVITIES            # 'agents.adapter.send_activities'
adapter_constants.SPAN_UPDATE_ACTIVITY            # 'agents.adapter.update_activity'
adapter_constants.SPAN_DELETE_ACTIVITY            # 'agents.adapter.delete_activity'
adapter_constants.SPAN_CONTINUE_CONVERSATION      # 'agents.adapter.continue_conversation'
adapter_constants.SPAN_CREATE_CONNECTOR_CLIENT    # 'agents.adapter.create_connector_client'
adapter_constants.SPAN_CREATE_USER_TOKEN_CLIENT   # 'agents.adapter.create_user_token_client'
adapter_constants.SPAN_WRITE_RESPONSE             # 'agents.adapter.write_response'

# AgentApplication
app_constants.SPAN_ON_TURN                        # 'agents.app.run'
app_constants.SPAN_ROUTE_HANDLER                  # 'agents.app.route_handler'
app_constants.SPAN_BEFORE_TURN                    # 'agents.app.before_turn'
app_constants.SPAN_AFTER_TURN                     # 'agents.app.after_turn'
app_constants.SPAN_DOWNLOAD_FILES                 # 'agents.app.download_files'

# TurnContext
turn_context_constants.SPAN_TURN_SEND_ACTIVITIES  # 'agents.turn.send_activities'

# ConnectorClient
connector_constants.SPAN_REPLY_TO_ACTIVITY        # 'agents.connector.reply_to_activity'
connector_constants.SPAN_SEND_TO_CONVERSATION     # 'agents.connector.send_to_conversation'
connector_constants.SPAN_UPDATE_ACTIVITY          # 'agents.connector.update_activity'
connector_constants.SPAN_DELETE_ACTIVITY          # 'agents.connector.delete_activity'
connector_constants.SPAN_CREATE_CONVERSATION      # 'agents.connector.create_conversation'
connector_constants.SPAN_GET_CONVERSATIONS        # 'agents.connector.get_conversations'
connector_constants.SPAN_GET_CONVERSATION_MEMBERS # 'agents.connector.get_conversation_members'
connector_constants.SPAN_UPLOAD_ATTACHMENT        # 'agents.connector.upload_attachment'
connector_constants.SPAN_GET_ATTACHMENT           # 'agents.connector.get_attachment'
connector_constants.SPAN_GET_ATTACHMENT_INFO      # 'agents.connector.get_attachment_info'

# Storage
storage_constants.SPAN_STORAGE_READ               # 'agents.storage.read'
storage_constants.SPAN_STORAGE_WRITE              # 'agents.storage.write'
storage_constants.SPAN_STORAGE_DELETE             # 'agents.storage.delete'

# Authentication
auth_constants.SPAN_GET_ACCESS_TOKEN              # 'agents.authentication.get_access_token'
auth_constants.SPAN_ACQUIRE_TOKEN_ON_BEHALF_OF    # 'agents.authentication.acquire_token_on_behalf_of'
auth_constants.SPAN_GET_AGENTIC_INSTANCE_TOKEN    # 'agents.authentication.get_agentic_instance_token'
auth_constants.SPAN_GET_AGENTIC_USER_TOKEN        # 'agents.authentication.get_agentic_user_token'

# Authorization
oauth_constants.AGENTIC_TOKEN                     # 'agents.authorization.agentic_token'
oauth_constants.AZURE_BOT_TOKEN                   # 'agents.authorization.azure_bot_token'
oauth_constants.AZURE_BOT_SIGN_IN                 # 'agents.authorization.azure_bot_signin'
oauth_constants.AZURE_BOT_SIGN_OUT                # 'agents.authorization.azure_bot_signout'

# UserTokenClient
connector_constants.SPAN_GET_USER_TOKEN                  # 'agents.user_token_client.get_user_token'
connector_constants.SPAN_SIGN_OUT                        # 'agents.user_token_client.sign_out'
connector_constants.SPAN_EXCHANGE_TOKEN                  # 'agents.user_token_client.exchange_token'
connector_constants.SPAN_GET_TOKEN_OR_SIGN_IN_RESOURCE   # 'agents.user_token_client.get_token_or_sign_in_resource'
connector_constants.SPAN_GET_TOKEN_STATUS                # 'agents.user_token_client.get_token_status'
connector_constants.SPAN_GET_AAD_TOKENS                  # 'agents.user_token_client.get_aad_tokens'
```

---

## Python Metric Constants Reference

```python
from microsoft_agents.hosting.core.telemetry.adapter import constants as adapter_constants
from microsoft_agents.hosting.core.app.telemetry import constants as app_constants
from microsoft_agents.hosting.core.connector.telemetry import constants as connector_constants
from microsoft_agents.hosting.core.storage.telemetry import constants as storage_constants
from microsoft_agents.hosting.core.authorization.telemetry import constants as auth_constants

# Adapter
adapter_constants.METRIC_ADAPTER_PROCESS_DURATION  # 'agents.adapter.process.duration'
adapter_constants.METRIC_ACTIVITIES_RECEIVED        # 'agents.activities.received'
adapter_constants.METRIC_ACTIVITIES_SENT            # 'agents.activities.sent'
adapter_constants.METRIC_ACTIVITIES_UPDATED         # 'agents.activities.updated'
adapter_constants.METRIC_ACTIVITIES_DELETED         # 'agents.activities.deleted'

# Turn
app_constants.METRIC_TURN_COUNT                     # 'agents.turn.count'
app_constants.METRIC_TURN_ERROR_COUNT               # 'agents.turn.error.count'
app_constants.METRIC_TURN_DURATION                  # 'agents.turn.duration'

# Storage
storage_constants.METRIC_STORAGE_OPERATION_TOTAL    # 'agents.storage.operation.total'
storage_constants.METRIC_STORAGE_OPERATION_DURATION # 'agents.storage.operation.duration'

# Connector
connector_constants.METRIC_CONNECTOR_REQUEST_COUNT          # 'agents.connector.request.count'
connector_constants.METRIC_CONNECTOR_REQUEST_DURATION       # 'agents.connector.request.duration'

# UserTokenClient
connector_constants.METRIC_USER_TOKEN_CLIENT_REQUEST_COUNT      # 'agents.user_token_client.request.count'
connector_constants.METRIC_USER_TOKEN_CLIENT_REQUEST_DURATION   # 'agents.user_token_client.request.duration'

# Authentication
auth_constants.METRIC_AUTH_TOKEN_REQUEST_COUNT      # 'agents.auth.token.request.count'
auth_constants.METRIC_AUTH_TOKEN_REQUEST_DURATION   # 'agents.auth.token.duration'
```

---

# Appendix: Common Span Attributes

These attributes appear across multiple spans and follow OpenTelemetry semantic conventions where applicable.

| Attribute | Type | Description |
|-----------|------|-------------|
| `activity.type` | string | Bot Framework activity type (message, conversationUpdate, invoke, etc.) |
| `activity.id` | string | Unique identifier for the activity |
| `activity.channel_id` | string | Channel identifier (msteams, webchat, directline, etc.) |
| `activity.conversation_id` | string | Unique identifier for the conversation |
| `activity.is_agentic` | boolean | Whether this is an agentic (agent-to-agent) request |
| `activity.delivery_mode` | string | The delivery mode of the activity |
| `auth.scope` | string | Authentication scope for token requests |
| `auth.scopes` | string[] | Authentication scopes for multi-scope requests |
| `auth.method` | string | Authentication method used (secret, certificate, managed_identity, etc.) |
| `auth.connection.name` | string | The OAuth connection name |
| `auth.handler.id` | string | The authorization handler identifier |
| `error.type` | string | Exception/error class name when an error occurs |
| `http.method` | string | HTTP method for outbound requests |
| `http.status_code` | string | HTTP response status code |
| `user.id` | string | The user identifier |

---

# Appendix: Span Kind Reference

The SDK uses OpenTelemetry SpanKind to categorize spans:

| Kind | Value | Usage |
|------|-------|-------|
| `INTERNAL` | 0 | Internal operations within the SDK |
| `SERVER` | 1 | Handling incoming requests (e.g., adapter.process) |
| `CLIENT` | 2 | Outbound calls (e.g., connector requests, agent-to-agent calls) |
| `PRODUCER` | 3 | Producing messages to a broker |
| `CONSUMER` | 4 | Consuming messages from a broker |

---

*This documentation is current as of the latest version of the Microsoft 365 Agents SDK. For the most up-to-date information, refer to the official SDK documentation and release notes.*

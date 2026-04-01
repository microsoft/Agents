# M365 Agents SDK Telemetry

OpenTelemetry traces, metrics, and span attributes for the M365 Agents SDK across C#, Python, and JavaScript.

The M365 Agents SDK provides built-in OpenTelemetry instrumentation to help developers monitor and debug their agent applications. When OpenTelemetry is configured in your application, the SDK automatically emits traces and metrics for key operations.

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
- [Coming Soon](#c-telemetry)

### Python Telemetry
- [Coming Soon](#python-telemetry)

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

#### agents.authorization.azure_bot_signout

Span for signing out a user via Azure Bot Service.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.handler.id` | string | The authorization handler identifier |
| `auth.connection.name` | string | The connection name used |
| `activity.channel_id` | string | The channel identifier |

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

#### agents.user_token_client.get_sign_in_resource

Span for getting a sign-in resource.

| Attribute | Type | Description |
|-----------|------|-------------|
| `auth.connection.name` | string | The connection name |

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
SpanNames.USER_TOKEN_CLIENT_GET_SIGN_IN_RESOURCE        // 'agents.user_token_client.get_sign_in_resource'
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

*This section will document OpenTelemetry traces and metrics for the C# M365 Agents SDK.*

**Coming Soon**

The C# SDK telemetry implementation is under development. This section will include:

- Trace span names and attributes
- Metric names and dimensions
- Configuration guidance for .NET applications
- Integration with Azure Monitor and Application Insights

---

# Python Telemetry

*This section will document OpenTelemetry traces and metrics for the Python M365 Agents SDK.*

**Coming Soon**

The Python SDK telemetry implementation is under development. This section will include:

- Trace span names and attributes
- Metric names and dimensions
- Configuration guidance for Python applications
- Integration with popular Python observability tools

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

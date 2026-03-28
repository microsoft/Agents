# A2A Transport Patterns

This document explains the four Agent-to-Agent (A2A) transport patterns implemented in this reference, with protocol-level details and sequence diagrams.

---

## Overview

| Pattern | Transport | Latency | Use Case |
|---------|-----------|---------|----------|
| **Sync (Ping)** | HTTP POST → JSON response | Blocks until complete | Simple queries, testing |
| **SSE Streaming** | HTTP POST → `text/event-stream` | Real-time chunks | UX with progress updates |
| **Push Notification** | HTTP POST → webhook callback | Async, fire-and-forget | Background tasks, long jobs |
| **Webhook Status** | GET → accumulated events | On-demand polling | Monitoring, audit trails |

---

## Pattern 1: Synchronous (message/send)

**How it works**: Client sends a JSON-RPC request, server blocks until the full response is ready, then returns it.

```
Client                          ADK Agent
  │                                │
  │─── POST /  ────────────────────▶│
  │    {"jsonrpc":"2.0",           │
  │     "method":"message/send",   │
  │     "params":{"message":{...}}}│
  │                                │── Agent processes ──▶
  │                                │                     │
  │◀── 200 OK ─────────────────────│◀────────────────────│
  │    {"result":{"id":"task-1",   │
  │     "status":{"state":"completed"},
  │     "artifacts":[{...}]}}      │
```

**JSON-RPC Request**:
```json
{
  "jsonrpc": "2.0",
  "id": "req-001",
  "method": "message/send",
  "params": {
    "message": {
      "role": "user",
      "parts": [{"text": "Analyze Nike brand keywords"}],
      "messageId": "msg-001"
    }
  }
}
```

**JSON-RPC Response**:
```json
{
  "jsonrpc": "2.0",
  "id": "req-001",
  "result": {
    "id": "task-abc123",
    "status": {
      "state": "completed",
      "message": {
        "role": "agent",
        "parts": [{"text": "Here is the brand analysis..."}]
      }
    },
    "artifacts": [
      {
        "parts": [{"text": "Detailed analysis content..."}]
      }
    ]
  }
}
```

**When to use**: Simple request-response patterns, testing, or when the client can afford to wait.

---

## Pattern 2: SSE Streaming (message/stream)

**How it works**: Client sends a request; server responds with a `text/event-stream` that delivers incremental status updates as the agent progresses through states.

```
Client                          ADK Agent
  │                                │
  │─── POST /  ────────────────────▶│
  │    {"method":"message/stream"} │
  │                                │
  │◀── SSE: submitted ────────────│
  │◀── SSE: working ──────────────│
  │◀── SSE: working (artifact) ───│
  │◀── SSE: completed ────────────│
  │◀── [stream closed] ───────────│
```

**SSE Event Format**:
```
data: {"jsonrpc":"2.0","id":"req-001","result":{"id":"task-xyz","status":{"state":"submitted"}}}

data: {"jsonrpc":"2.0","id":"req-001","result":{"id":"task-xyz","status":{"state":"working","message":{"role":"agent","parts":[{"text":"Searching..."}]}}}}

data: {"jsonrpc":"2.0","id":"req-001","result":{"id":"task-xyz","status":{"state":"completed"},"artifacts":[...]}}
```

**Key differences from sync**:
- Method is `message/stream` instead of `message/send`
- Response is `Content-Type: text/event-stream`
- Multiple events arrive incrementally
- Client sees state transitions in real time

**When to use**: UIs that need progress indicators, long-running analyses, or when you want to show partial results.

---

## Pattern 3: Push Notification (webhook callback)

**How it works**: Client registers a webhook URL alongside the message. Server processes asynchronously and POSTs the result to the webhook when done.

```
Client                          ADK Agent           Webhook Server
  │                                │                      │
  │─── POST / (message/send) ──────▶│                      │
  │    + pushNotificationConfig    │                      │
  │    {url: "http://host/webhook"}│                      │
  │                                │                      │
  │◀── 200 OK (task submitted) ────│                      │
  │                                │                      │
  │                                │── Process async ──▶  │
  │                                │                      │
  │                                │── POST /webhook ────▶│
  │                                │   {task result}      │
  │                                │                      │
  │─── GET /webhook/status ────────────────────────────▶│
  │◀── {notifications received} ───────────────────────│
```

**Registration** (inline with message):
```json
{
  "jsonrpc": "2.0",
  "id": "req-push",
  "method": "message/send",
  "params": {
    "message": {
      "role": "user",
      "parts": [{"text": "Analyze Adidas brand"}],
      "messageId": "msg-push"
    },
    "pushNotificationConfig": {
      "url": "http://localhost:3978/a2a/webhook",
      "authentication": null
    }
  }
}
```

**Alternatively, register after task creation**:
```json
{
  "jsonrpc": "2.0",
  "id": "req-push-config",
  "method": "tasks/pushNotificationConfig/set",
  "params": {
    "taskId": "task-abc123",
    "pushNotificationConfig": {
      "url": "http://localhost:3978/a2a/webhook"
    }
  }
}
```

> **Note**: The A2A library uses the `tasks/` prefix for the method name (not just `pushNotificationConfig/set`), and the field is `taskId` (not `id`).

**Webhook Payload** (what the server POSTs to your callback):
```json
{
  "jsonrpc": "2.0",
  "method": "tasks/pushNotification/send",
  "params": {
    "taskId": "task-abc123",
    "status": {
      "state": "completed",
      "message": {
        "role": "agent",
        "parts": [{"text": "Analysis complete..."}]
      }
    }
  }
}
```

**When to use**: Long-running tasks, batch processing, or when the client shouldn't hold an HTTP connection open.

---

## Pattern 4: Webhook Status (polling)

**How it works**: After registering push notifications, the client can poll the webhook receiver to see all accumulated notifications.

```
Client                          Webhook Server
  │                                │
  │─── GET /a2a/webhook ──────────▶│
  │                                │
  │◀── 200 OK ─────────────────────│
  │    {"total": 3,                │
  │     "notifications": [...]}    │
```

**Response**:
```json
{
  "total": 2,
  "notifications": [
    {
      "taskId": "task-abc",
      "state": "working",
      "timestamp": "2025-01-15T10:30:00Z"
    },
    {
      "taskId": "task-abc",
      "state": "completed",
      "timestamp": "2025-01-15T10:30:07Z",
      "has_message": true
    }
  ]
}
```

**When to use**: Monitoring dashboards, audit logs, or debugging push notification delivery.

---

## Agent Card Discovery

Before using any pattern, clients should discover the agent's capabilities:

```
GET /.well-known/agent-card.json
```

**Response**:
```json
{
  "name": "Brand Search Optimization Agent",
  "description": "Analyzes brand keywords, search rankings, and competitive positioning",
  "url": "http://localhost:8080",
  "version": "1.0.0",
  "capabilities": {
    "streaming": true,
    "pushNotifications": true,
    "stateTransitionHistory": true
  },
  "skills": [
    {
      "id": "brand-search-optimization",
      "name": "Brand Search Optimization",
      "description": "Analyzes brand search performance..."
    }
  ]
}
```

The `capabilities` object tells the client which patterns are available. A well-behaved client should check these before attempting streaming or push notifications.

---

## State Machine

All patterns share the same task state machine:

```
submitted → working → completed
                   ↘ failed
                   ↘ canceled
                   ↘ input-required
```

- **submitted**: Task received and queued
- **working**: Agent actively processing (may appear multiple times with SSE)
- **completed**: Final response ready with artifacts
- **failed**: Processing error occurred
- **canceled**: Task was canceled
- **input-required**: Agent needs more information from the user (multi-turn)

---

## Implementation Notes

### CLI Test Client (`cli_test.py`)

The CLI client implements all 4 patterns in ~250 lines of Python. Key implementation details:

- **httpx** for HTTP client (async-capable, streaming-capable)
- **SSE parsing**: Manual line-by-line parsing of `data:` prefixed events
- **Push notifications**: Registers inline `pushNotificationConfig` with the message
- **Webhook server**: Runs as part of the A2A client agent on `/a2a/webhook`

### A2A Client Library (`brand_intelligence_advisor/tools/a2a_client.py`)

The reusable A2A client wraps all patterns into clean async methods:

```python
from brand_intelligence_advisor.tools.a2a_client import A2AClient

client = A2AClient("http://localhost:8080")

# Discover
card = await client.discover()

# Sync (ping)
result = await client.send_message("Analyze Nike")

# Stream (SSE)
async for event in client.stream_message("Analyze Nike"):
    print(event.text)

# Push (webhook)
result = await client.send_with_push("Analyze Nike", "http://localhost:3978/a2a/webhook")
```

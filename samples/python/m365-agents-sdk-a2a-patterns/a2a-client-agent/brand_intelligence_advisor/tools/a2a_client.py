# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
A2A Protocol Client — Implements all communication patterns for
interacting with a remote A2A-compliant agent (Google ADK Brand Search
Optimization Agent).

Patterns:
  1. Ping (message/send)        — Synchronous blocking request/response
  2. SSE  (message/stream)      — Server-Sent Events streaming
  3. Push (pushNotificationConfig/set + message/send) — Webhook-based async
  4. Webhook receiver            — Handled server-side in server.py

Usage:
    from brand_intelligence_advisor.tools.a2a_client import A2AClient

    client = A2AClient("http://localhost:8080")
    card   = await client.discover()     # fetch agent card
    task   = await client.send_message("Analyze Nike")  # ping
    async for event in client.stream_message("Analyze Adidas"):  # stream
        print(event.text)
    task   = await client.send_with_push("Analyze Puma", webhook_url)  # push
"""

import json
import uuid
import logging
from typing import AsyncIterator, Optional
from dataclasses import dataclass, field

import httpx

logger = logging.getLogger(__name__)


# ── Data Classes ──────────────────────────────────────────────────────────────

@dataclass
class AgentCard:
    """Discovered remote agent metadata from /.well-known/agent-card.json."""
    name: str
    description: str
    url: str
    version: str
    capabilities: dict = field(default_factory=dict)
    skills: list = field(default_factory=list)


@dataclass
class A2ATask:
    """Represents a task returned from the A2A agent."""
    task_id: str
    context_id: str
    status: str  # submitted, working, completed, failed, canceled
    message: Optional[str] = None
    artifacts: list = field(default_factory=list)


@dataclass
class SSEEvent:
    """A single Server-Sent Event from the A2A stream."""
    event_type: str  # status, artifact, error
    data: dict = field(default_factory=dict)
    text: Optional[str] = None


# ── A2A Client ────────────────────────────────────────────────────────────────

class A2AClient:
    """
    Client for the A2A (Agent-to-Agent) protocol v0.3.0.

    Supports all 4 communication patterns:
      - discover()        → GET /.well-known/agent-card.json
      - send_message()    → JSON-RPC message/send  (ping/sync)
      - stream_message()  → JSON-RPC message/stream (SSE)
      - register_push()   → JSON-RPC pushNotificationConfig/set

    All methods use JSON-RPC 2.0 over HTTP as specified by the A2A protocol.
    """

    def __init__(self, base_url: str, timeout: float = 120.0):
        """
        Args:
            base_url: Root URL of the A2A agent (e.g. http://localhost:8080).
            timeout:  HTTP timeout in seconds (default 120s for large analyses).
        """
        self.base_url = base_url.rstrip("/")
        self._client = httpx.AsyncClient(timeout=timeout)
        self._agent_card: Optional[AgentCard] = None

    async def close(self):
        """Close the underlying HTTP client."""
        await self._client.aclose()

    # ── 0. Agent Discovery ────────────────────────────────────────────────

    async def discover(self) -> AgentCard:
        """
        Fetch the remote agent's Agent Card from the well-known endpoint.
        This is the first step in any A2A interaction — discovering what
        the remote agent can do and which patterns it supports.
        """
        url = f"{self.base_url}/.well-known/agent-card.json"
        logger.info(f"Discovering agent at {url}")

        resp = await self._client.get(url)
        resp.raise_for_status()
        data = resp.json()

        self._agent_card = AgentCard(
            name=data.get("name", "Unknown"),
            description=data.get("description", ""),
            url=data.get("url", self.base_url),
            version=data.get("version", "unknown"),
            capabilities=data.get("capabilities", {}),
            skills=data.get("skills", []),
        )
        logger.info(f"Discovered: {self._agent_card.name} v{self._agent_card.version}")
        return self._agent_card

    # ── 1. Ping Mode (message/send) ──────────────────────────────────────

    async def send_message(
        self, text: str, context_id: Optional[str] = None
    ) -> A2ATask:
        """
        Send a synchronous (blocking) message to the A2A agent.
        This is the 'ping' pattern — sends a request and waits for the
        full response before returning.

        Best for: Quick lookups, single-brand checks, glossary queries.
        """
        context_id = context_id or str(uuid.uuid4())
        request_id = str(uuid.uuid4())

        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": "message/send",
            "params": {
                "message": {
                    "role": "user",
                    "parts": [{"kind": "text", "text": text}],
                    "messageId": str(uuid.uuid4()),
                    "contextId": context_id,
                },
            },
        }

        logger.info(f"[PING] Sending message/send (context={context_id[:8]}...)")
        resp = await self._client.post(self.base_url, json=payload)
        resp.raise_for_status()
        result = resp.json()

        return self._parse_task_response(result, context_id)

    # ── 2. SSE Mode (message/stream) ─────────────────────────────────────

    async def stream_message(
        self, text: str, context_id: Optional[str] = None
    ) -> AsyncIterator[SSEEvent]:
        """
        Send a message and receive Server-Sent Events (SSE) stream.
        Yields SSEEvent objects as they arrive from the remote agent.

        Best for: Detailed reports, real-time progress visibility.
        """
        context_id = context_id or str(uuid.uuid4())
        request_id = str(uuid.uuid4())

        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": "message/stream",
            "params": {
                "message": {
                    "role": "user",
                    "parts": [{"kind": "text", "text": text}],
                    "messageId": str(uuid.uuid4()),
                    "contextId": context_id,
                },
            },
        }

        logger.info(f"[SSE] Sending message/stream (context={context_id[:8]}...)")

        async with self._client.stream(
            "POST",
            self.base_url,
            json=payload,
            headers={"Accept": "text/event-stream"},
        ) as response:
            response.raise_for_status()

            event_type = ""
            data_lines = []

            async for line in response.aiter_lines():
                line = line.strip()

                if line.startswith("event:"):
                    event_type = line[6:].strip()
                elif line.startswith("data:"):
                    data_lines.append(line[5:].strip())
                elif line == "" and data_lines:
                    # End of event block — emit it
                    raw_data = "\n".join(data_lines)
                    data_lines = []

                    try:
                        parsed = json.loads(raw_data)
                    except json.JSONDecodeError:
                        parsed = {"raw": raw_data}

                    sse_event = SSEEvent(
                        event_type=event_type or "message",
                        data=parsed,
                        text=self._extract_text_from_event(parsed),
                    )
                    logger.debug(f"[SSE] Event: {sse_event.event_type}")
                    yield sse_event

                    event_type = ""

    # ── 3. Push Notification Config ──────────────────────────────────────

    async def register_push(
        self, task_id: str, webhook_url: str, token: Optional[str] = None
    ) -> dict:
        """
        Register a webhook URL to receive push notifications for a task.
        The remote agent will POST to this URL when the task completes.

        Best for: Long-running analyses where the user doesn't want to wait.
        """
        token = token or f"m365-push-{uuid.uuid4().hex[:12]}"
        request_id = str(uuid.uuid4())

        payload = {
            "jsonrpc": "2.0",
            "id": request_id,
            "method": "pushNotificationConfig/set",
            "params": {
                "taskId": task_id,
                "pushNotificationConfig": {
                    "url": webhook_url,
                    "token": token,
                },
            },
        }

        logger.info(f"[PUSH] Registering webhook for task {task_id[:8]}...")
        resp = await self._client.post(self.base_url, json=payload)
        resp.raise_for_status()
        result = resp.json()

        if "error" in result:
            logger.error(f"Push registration failed: {result['error']}")
            return {"success": False, "error": result["error"]}

        logger.info(f"Push notification registered -> {webhook_url}")
        return {"success": True, "token": token, "task_id": task_id}

    async def send_with_push(
        self, text: str, webhook_url: str, context_id: Optional[str] = None
    ) -> A2ATask:
        """
        Send a message and register a push notification webhook.
        Returns the initial task immediately (status updates arrive via webhook).

        This combines message/send + pushNotificationConfig/set into one call.
        """
        # First send the message
        task = await self.send_message(text, context_id)

        # Then register push for the task
        push_result = await self.register_push(task.task_id, webhook_url)
        if not push_result.get("success"):
            logger.warning("Push registration failed but message was sent")

        return task

    # ── Helpers ──────────────────────────────────────────────────────────

    def _parse_task_response(self, result: dict, context_id: str) -> A2ATask:
        """Parse a JSON-RPC response into an A2ATask dataclass."""
        if "error" in result:
            return A2ATask(
                task_id="",
                context_id=context_id,
                status="failed",
                message=f"Error: {result['error'].get('message', 'Unknown error')}",
            )

        task_data = result.get("result", {})
        task_id = task_data.get("id", "")
        status = task_data.get("status", {}).get("state", "unknown")

        # Extract text from artifacts
        text_parts = []
        for artifact in task_data.get("artifacts", []):
            for part in artifact.get("parts", []):
                if part.get("kind") == "text" or "text" in part:
                    text_parts.append(part.get("text", ""))

        # Also check status message
        status_msg = task_data.get("status", {}).get("message", {})
        if isinstance(status_msg, dict):
            for part in status_msg.get("parts", []):
                if "text" in part:
                    text_parts.append(part["text"])

        return A2ATask(
            task_id=task_id,
            context_id=context_id,
            status=status,
            message="\n".join(text_parts) if text_parts else None,
            artifacts=task_data.get("artifacts", []),
        )

    @staticmethod
    def _extract_text_from_event(data: dict) -> Optional[str]:
        """Extract readable text from an SSE event payload."""
        # Check result -> status -> message -> parts
        result = data.get("result", data)
        status = result.get("status", {})
        message = status.get("message", {})

        if isinstance(message, dict):
            parts = message.get("parts", [])
            texts = [p.get("text", "") for p in parts if "text" in p]
            if texts:
                return "\n".join(texts)

        # Check artifacts
        for artifact in result.get("artifacts", []):
            for part in artifact.get("parts", []):
                if "text" in part:
                    return part["text"]

        return None

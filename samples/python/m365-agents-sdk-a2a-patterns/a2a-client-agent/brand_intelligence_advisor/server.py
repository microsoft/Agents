# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Brand Intelligence Advisor — HTTP Server.

Runs an aiohttp server with two endpoints:
  - POST /api/messages   → M365 SDK message processing (user <-> agent)
  - POST /a2a/webhook    → A2A push notification receiver (ADK agent -> this agent)
  - GET  /a2a/webhook    → View received notifications (debug)

The M365 endpoint handles Teams / WebChat / CLI interactions, while the
webhook endpoint receives asynchronous push notifications from the remote
ADK agent when background analyses complete (A2A push pattern).
"""

import json
import logging
from os import environ
from datetime import datetime, timezone

from microsoft_agents.hosting.core import AgentApplication, AgentAuthConfiguration
from microsoft_agents.hosting.aiohttp import (
    start_agent_process,
    jwt_authorization_middleware,
    CloudAdapter,
)
from aiohttp.web import Request, Response, Application, run_app, json_response

logger = logging.getLogger(__name__)


def start_server(
    agent_application: AgentApplication,
    auth_configuration: AgentAuthConfiguration,
    push_notifications: list,
):
    """
    Start the aiohttp server with M365 message endpoint and A2A webhook.

    Args:
        agent_application: The configured M365 AgentApplication instance.
        auth_configuration: MSAL authentication configuration.
        push_notifications: Shared list that the webhook handler appends
                            incoming notifications to, so the agent can
                            surface them to users via the 'status' command.
    """

    # ── M365 SDK Message Endpoint ─────────────────────────────────────────

    async def messages_entry_point(req: Request) -> Response:
        """Handle incoming M365 SDK messages from users/channels."""
        agent: AgentApplication = req.app["agent_app"]
        adapter: CloudAdapter = req.app["adapter"]

        logger.info(f"M365 message received from {req.remote}")
        return await start_agent_process(req, agent, adapter)

    # ── A2A Webhook Endpoint (Push Notifications) ─────────────────────────

    async def a2a_webhook_handler(req: Request) -> Response:
        """
        Receive push notifications from the ADK agent.

        The ADK agent sends a JSON-RPC message when it finishes processing
        a background task (A2A push pattern). We parse the notification,
        store it, and make it available for the user via the 'status' command.
        """
        try:
            body = await req.json()
            logger.info("A2A push notification received!")
            logger.info(f"   Body: {json.dumps(body, indent=2)[:500]}")

            # Extract useful information — handle both JSON-RPC response
            # and notification formats from the ADK push sender
            token = req.headers.get("X-A2A-Notification-Token", "none")
            result = body.get("result", body.get("params", body))
            task_id = result.get("id", result.get("taskId", "unknown"))
            status_obj = result.get("status", {})
            status = (
                status_obj.get("state", "unknown")
                if isinstance(status_obj, dict)
                else str(status_obj)
            )

            # Extract text from artifacts or status message
            text_parts = []
            for artifact in result.get("artifacts", []):
                for part in artifact.get("parts", []):
                    if "text" in part:
                        text_parts.append(part["text"])

            status_msg = result.get("status", {}).get("message", {})
            if isinstance(status_msg, dict):
                for part in status_msg.get("parts", []):
                    if "text" in part:
                        text_parts.append(part["text"])

            notification = {
                "task_id": task_id,
                "status": status,
                "token": token,
                "text": " ".join(text_parts) if text_parts else "",
                "received_at": datetime.now(timezone.utc).strftime("%H:%M:%S UTC"),
                "raw": body,
            }

            push_notifications.append(notification)
            logger.info(
                f"Notification stored (total: {len(push_notifications)}) "
                f"| Task: {task_id[:12]}... | Status: {status}"
            )

            return json_response({"status": "received"}, status=200)

        except Exception as e:
            logger.error(f"Webhook handler error: {e}")
            return json_response({"error": str(e)}, status=500)

    async def a2a_webhook_list(req: Request) -> Response:
        """GET endpoint to inspect received push notifications (debug)."""
        return json_response(
            {
                "total": len(push_notifications),
                "notifications": [
                    {
                        "task_id": n["task_id"],
                        "status": n["status"],
                        "received_at": n["received_at"],
                        "text_preview": n.get("text", "")[:200],
                    }
                    for n in push_notifications
                ],
            }
        )

    # ── Application Setup ─────────────────────────────────────────────────

    # Use JWT middleware for authenticated mode, or no middleware for anonymous
    client_id = environ.get(
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID", ""
    )
    if client_id:
        middlewares = [jwt_authorization_middleware]
        logger.info("Running in authenticated mode (MSAL)")
    else:
        middlewares = []
        logger.info("Running in anonymous mode (no MSAL auth)")

    APP = Application(middlewares=middlewares)

    # M365 SDK endpoint
    APP.router.add_post("/api/messages", messages_entry_point)

    # A2A webhook endpoints
    APP.router.add_post("/a2a/webhook", a2a_webhook_handler)
    APP.router.add_get("/a2a/webhook", a2a_webhook_list)

    # Store references for handlers
    APP["agent_configuration"] = auth_configuration
    APP["agent_app"] = agent_application
    APP["adapter"] = agent_application.adapter

    host = environ.get("AGENT_HOST", "localhost")
    port = int(environ.get("AGENT_PORT", "3978"))

    logger.info(f"Starting Brand Intelligence Advisor on http://{host}:{port}")
    logger.info(f"   M365 messages:     POST http://{host}:{port}/api/messages")
    logger.info(f"   A2A webhook:       POST http://{host}:{port}/a2a/webhook")
    logger.info(f"   Webhook status:    GET  http://{host}:{port}/a2a/webhook")

    try:
        run_app(APP, host=host, port=port)
    except Exception as error:
        raise error

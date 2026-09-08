# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
from functools import wraps
from os import environ
from typing import Any, Awaitable, Callable

from aiohttp import web
from microsoft_agents.activity import Activity, ConversationReference
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_decorator,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication, AgentAuthConfiguration
from microsoft_agents.hosting.core.app.proactive import Conversation


def _is_development() -> bool:
    return environ.get("ENVIRONMENT", "production").lower() == "development"


def _allowed_callers() -> set[str]:
    return {
        value.strip()
        for value in environ.get("ALLOWED_CALLERS", "").split(",")
        if value.strip()
    }


def _caller_is_allowed(request: web.Request, allowed_callers: set[str]) -> bool:
    identity = request.get("claims_identity")
    claims = identity.claims if identity else {}
    caller_id = claims.get("appid") or claims.get("azp") or ""
    return caller_id in allowed_callers


async def _read_json(request: web.Request) -> dict[str, Any]:
    try:
        payload = await request.json()
    except (json.JSONDecodeError, TypeError, ValueError):
        return {}
    return payload if isinstance(payload, dict) else {}


def create_app(
    agent_application: AgentApplication,
    auth_configuration: AgentAuthConfiguration,
) -> web.Application:
    is_development = _is_development()
    allowed_callers = _allowed_callers()
    if not is_development and not allowed_callers:
        raise RuntimeError(
            "ALLOWED_CALLERS must contain at least one caller app ID "
            "outside Development."
        )

    adapter: CloudAdapter = agent_application.adapter

    async def handle_root(_request: web.Request) -> web.Response:
        return web.json_response({"status": "ready", "sample": "proactive"})

    async def handle_messages(request: web.Request) -> web.Response:
        response = await start_agent_process(request, agent_application, adapter)
        return response or web.Response(status=202)

    async def handle_send_activity(request: web.Request) -> web.Response:
        conversation_id = request.match_info["conversation_id"]
        payload = await _read_json(request)
        if not payload:
            return web.json_response(
                {"error": "The request body must be an activity object."}, status=400
            )

        try:
            activity = Activity.model_validate(payload)
            await agent_application.proactive.send_activity(
                adapter, conversation_id, activity
            )
        except KeyError:
            return web.json_response(
                {
                    "error": (
                        f"Conversation '{conversation_id}' was not found. "
                        "Send -s first to store it."
                    )
                },
                status=404,
            )
        except ValueError as error:
            return web.json_response({"error": str(error)}, status=400)
        except Exception:  # noqa: BLE001
            return web.json_response(
                {"error": "The proactive activity could not be sent."}, status=500
            )

        return web.json_response(
            {"status": "ok", "conversationId": conversation_id}, status=200
        )

    async def handle_continue(request: web.Request) -> web.Response:
        payload = await _read_json(request)
        if not payload.get("reference") or not payload.get("claims"):
            return web.json_response(
                {"error": "The request body must contain 'reference' and 'claims'."},
                status=400,
            )

        try:
            conversation = Conversation(
                claims=payload["claims"],
                conversation_reference=ConversationReference.model_validate(
                    payload["reference"]
                ),
            )
            conversation.validate()
            from .agent import on_continue_conversation

            await agent_application.proactive.continue_conversation(
                adapter, conversation, on_continue_conversation
            )
        except (TypeError, ValueError):
            return web.json_response(
                {"error": "The conversation data is invalid."}, status=400
            )
        except Exception:  # noqa: BLE001
            return web.json_response(
                {"error": "The conversation could not be continued."}, status=500
            )

        return web.json_response(
            {
                "status": "ok",
                "conversationId": conversation.conversation_reference.conversation.id,
            },
            status=200,
        )

    def protect_proactive_route(
        handler: Callable[[web.Request], Awaitable[web.Response]],
    ) -> Callable[[web.Request], Awaitable[web.Response]]:
        if is_development:
            return handler

        @wraps(handler)
        async def require_allowed_caller(request: web.Request) -> web.Response:
            if not _caller_is_allowed(request, allowed_callers):
                return web.json_response(
                    {"error": "The caller is not in the allowed callers list."},
                    status=403,
                )
            return await handler(request)

        return jwt_authorization_decorator(require_allowed_caller)

    # The SDK validates JWTs before processing messages.
    protected_messages = jwt_authorization_decorator(handle_messages)
    protected_send = protect_proactive_route(handle_send_activity)
    protected_continue = protect_proactive_route(handle_continue)

    app = web.Application()
    app["agent_configuration"] = auth_configuration
    app["agent_app"] = agent_application
    app["adapter"] = adapter
    app.router.add_get("/", handle_root)
    app.router.add_post("/api/messages", protected_messages)
    app.router.add_post(
        "/proactive/sendActivity/{conversation_id}", protected_send
    )
    app.router.add_post("/proactive/continue", protected_continue)
    return app


def start_server(
    agent_application: AgentApplication,
    auth_configuration: AgentAuthConfiguration,
) -> None:
    web.run_app(
        create_app(agent_application, auth_configuration),
        host=environ.get("HOST", "localhost"),
        port=int(environ.get("PORT", "3978")),
    )

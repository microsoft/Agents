# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
from typing import Callable

from aiohttp import web
from microsoft_agents.hosting.aiohttp import (
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication


def start_server(agent_factory: Callable[[], AgentApplication]) -> None:
    """Serve readiness without credentials; build the agent on first message request."""

    @web.middleware
    async def authorize(request: web.Request, handler):
        # Only the readiness probe skips JWT validation; every other request
        # (including /api/messages) is delegated to the SDK middleware.
        if request.method == "GET" and request.path == "/":
            return await handler(request)
        if "agent_app" not in request.app:
            try:
                application = agent_factory()
            except Exception as error:
                raise web.HTTPServiceUnavailable(
                    text="Agent connection settings are not configured."
                ) from error
            request.app["agent_app"] = application
            request.app["adapter"] = application.adapter
            request.app["agent_configuration"] = (
                application.connection_manager.get_default_connection_configuration()
            )
        return await jwt_authorization_middleware(request, handler)

    async def readiness(_request: web.Request) -> web.Response:
        return web.Response(text="Agent is ready.")

    async def messages(request: web.Request) -> web.Response:
        return await start_agent_process(
            request, request.app["agent_app"], request.app["adapter"]
        )

    async def close_agent(app: web.Application) -> None:
        application = app.get("agent_app")
        if application is not None:
            await application.close_background_tasks()

    host = web.Application(middlewares=[authorize])
    host.router.add_get("/", readiness)
    host.router.add_post("/api/messages", messages)
    host.on_cleanup.append(close_agent)
    web.run_app(host, host="localhost", port=int(os.environ.get("PORT", "3978")))

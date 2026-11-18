# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""HTTP hosting for the Agents SDK application."""

from os import environ
from collections.abc import Callable

from aiohttp import web
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication


def start_server(
    agent_factory: Callable[[], AgentApplication],
) -> None:
    @web.middleware
    async def readiness_aware_authorization(
        request: web.Request, handler
    ) -> web.StreamResponse:
        if request.method == "GET" and request.path == "/":
            return web.Response(text="Agent Cards is ready.")

        agent: AgentApplication | None = request.app.get("agent_app")
        if agent is None:
            agent = agent_factory()
            request.app["agent_app"] = agent
            request.app["adapter"] = agent.adapter
            request.app["agent_configuration"] = (
                agent.connection_manager.get_default_connection_configuration()
            )

        return await jwt_authorization_middleware(request, handler)

    async def process_message(request: web.Request) -> web.Response:
        app: AgentApplication = request.app["agent_app"]
        adapter: CloudAdapter = request.app["adapter"]
        return await start_agent_process(request, app, adapter)

    http_app = web.Application(middlewares=[readiness_aware_authorization])
    http_app.router.add_get("/", lambda _request: web.Response(text="Agent Cards is ready."))
    http_app.router.add_post("/api/messages", process_message)

    web.run_app(http_app, host="0.0.0.0", port=int(environ.get("PORT", "3978")))

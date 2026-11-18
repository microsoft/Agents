# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Authenticated aiohttp hosting for the Teams agent."""

from os import environ
from typing import Callable

from aiohttp import web
from microsoft_agents.hosting.aiohttp import jwt_authorization_middleware, start_agent_process
from microsoft_agents.hosting.core import AgentApplication


def create_web_app(
    agent_factory: Callable[[], AgentApplication],
) -> web.Application:
    @web.middleware
    async def authorize_except_readiness(request: web.Request, handler):
        if request.method == "GET" and request.path == "/":
            return web.Response(text="AI Messages agent is ready.")
        if "agent_app" not in request.app:
            agent = agent_factory()
            request.app["agent_app"] = agent
            request.app["adapter"] = agent.adapter
            request.app["agent_configuration"] = (
                agent.connection_manager.get_default_connection_configuration()
            )
        return await jwt_authorization_middleware(request, handler)

    async def readiness(_request: web.Request) -> web.Response:
        return web.Response(text="AI Messages agent is ready.")

    async def messages(request: web.Request) -> web.Response:
        agent = request.app["agent_app"]
        adapter = request.app["adapter"]
        return await start_agent_process(request, agent, adapter)

    app = web.Application(middlewares=[authorize_except_readiness])
    app.router.add_get("/", readiness)
    app.router.add_post("/api/messages", messages)
    return app


def start_server(agent_factory: Callable[[], AgentApplication]) -> None:
    web.run_app(
        create_web_app(agent_factory),
        host="localhost",
        port=int(environ.get("PORT", "3978")),
    )

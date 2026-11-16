# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ

from aiohttp.web import Application, Request, Response, run_app
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.activity.config import Configuration
from microsoft_agents.hosting.core import AgentApplication


def start_server(
    agent_application: AgentApplication, adapter: CloudAdapter, config: Configuration
) -> None:
    async def entry_point(request: Request) -> Response:
        return await start_agent_process(
            request, request.app["agent_app"], request.app["adapter"]
        )

    app = Application(middlewares=[jwt_authorization_middleware])
    app.router.add_post("/api/messages", entry_point)
    app["agent_configuration"] = config
    app["agent_app"] = agent_application
    app["adapter"] = adapter
    run_app(app, host="localhost", port=int(environ.get("PORT", "3978")))

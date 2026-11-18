# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""HTTP hosting with public readiness and authenticated message processing."""

import logging
from os import environ

from aiohttp.web import Application, Request, Response, run_app
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication

from .agent import create_agent


def start_server() -> None:
    http_app = Application()
    http_app["agent_app"] = None
    http_app["adapter"] = None

    async def readiness(_request: Request) -> Response:
        return Response(text="Ready")

    async def messages(request: Request) -> Response:
        agent: AgentApplication | None = request.app["agent_app"]
        if agent is None:
            configuration = load_configuration_from_env(environ)
            connection_manager = MsalConnectionManager(**configuration)
            agent = create_agent({"connection_manager": connection_manager})
            request.app["agent_app"] = agent
            request.app["adapter"] = agent.adapter
            request.app["agent_configuration"] = (
                connection_manager.get_default_connection_configuration()
            )

        async def process_message(authorized_request: Request) -> Response:
            payload = await authorized_request.json()
            logging.getLogger(__name__).info(
                "Received Teams activity: type=%s name=%s",
                payload.get("type"), payload.get("name"),
            )
            current_agent: AgentApplication = authorized_request.app["agent_app"]
            adapter: CloudAdapter = authorized_request.app["adapter"]
            return await start_agent_process(
                authorized_request,
                current_agent,
                adapter,
            )

        return await jwt_authorization_middleware(request, process_message)

    http_app.router.add_get("/", readiness)
    http_app.router.add_post("/api/messages", messages)
    run_app(http_app, host="localhost", port=int(environ.get("PORT", "3978")))

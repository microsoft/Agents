# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
import os
from pathlib import Path

from aiohttp import web
from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.hosting.core import (
    AgentApplication,
    AgentAuthConfiguration,
)

from .agent import create_agent


ROOT = Path(__file__).resolve().parent.parent
load_dotenv(ROOT / ".env")


def create_server(
    agent: AgentApplication | None = None,
    auth_configuration: AgentAuthConfiguration | None = None,
) -> web.Application:
    server = web.Application()
    message_api = web.Application(middlewares=[jwt_authorization_middleware])

    async def readiness(_request: web.Request) -> web.Response:
        return web.Response(text="Agent is ready")

    async def process_message(request: web.Request) -> web.Response:
        application: AgentApplication | None = server["agent_app"]
        if application is None:
            application = create_agent()
            server["agent_app"] = application
            message_api["agent_app"] = application
            message_api["adapter"] = application.adapter
        adapter: CloudAdapter = message_api["adapter"]
        return await start_agent_process(request, application, adapter)

    async def custom_form(_request: web.Request) -> web.FileResponse:
        return web.FileResponse(ROOT / "pages" / "CustomForm" / "index.html")

    server.router.add_get("/", readiness)
    server.router.add_get("/customform", custom_form)
    server["agent_app"] = agent
    message_api.router.add_post("/messages", process_message)
    message_api["agent_app"] = agent
    message_api["adapter"] = agent.adapter if agent is not None else None
    message_api["agent_configuration"] = auth_configuration
    server.add_subapp("/api", message_api)
    return server


async def start_server() -> None:
    required_settings = (
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID",
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET",
        "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID",
    )
    auth_configuration = None
    agent = None
    if all(os.getenv(setting) for setting in required_settings):
        configuration = load_configuration_from_env(os.environ)
        connection_manager = MsalConnectionManager(**configuration)
        auth_configuration = (
            connection_manager.get_default_connection_configuration()
        )
        agent = create_agent({"connection_manager": connection_manager})
    server = create_server(agent, auth_configuration)
    runner = web.AppRunner(server)
    await runner.setup()
    site = web.TCPSite(runner, host="0.0.0.0", port=int(os.getenv("PORT", "3978")))
    await site.start()
    try:
        await asyncio.Event().wait()
    finally:
        await runner.cleanup()

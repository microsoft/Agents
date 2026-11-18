# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Aiohttp hosting with public readiness and authenticated message processing."""

from os import environ

from aiohttp import web
from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_decorator,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication, AgentAuthConfiguration
from microsoft_agents.hosting.core.authorization import AuthTypes

from .agent import create_agent


def create_web_app() -> web.Application:
    load_dotenv()
    configuration = load_configuration_from_env(environ)
    connection_manager: MsalConnectionManager | None = None
    has_service_connection = any(
        environ.get(key)
        for key in (
            "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID",
            "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET",
            "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID",
        )
    )
    if has_service_connection:
        connection_manager = MsalConnectionManager(**configuration)
        auth_configuration = (
            connection_manager.get_default_connection_configuration()
        )
    else:
        auth_configuration = AgentAuthConfiguration(
            auth_type=AuthTypes.client_secret,
            anonymous_allowed=False,
            validate_issuer=True,
            issuers=["https://api.botframework.com"],
        )
    state: dict[str, AgentApplication | None] = {"agent": None}

    async def ready(_request: web.Request) -> web.Response:
        return web.json_response({"status": "ready", "agent": "attachments"})

    async def messages(request: web.Request) -> web.Response:
        agent = state["agent"]
        if agent is None:
            dependencies = (
                {"connection_manager": connection_manager}
                if connection_manager is not None
                else None
            )
            agent = create_agent(dependencies)
            state["agent"] = agent
        adapter: CloudAdapter = agent.adapter
        response = await start_agent_process(request, agent, adapter)
        return response or web.Response(status=202)

    async def cleanup(_application: web.Application) -> None:
        agent = state["agent"]
        if agent is not None:
            await agent.wait_for_uploads()

    application = web.Application()
    application["agent_configuration"] = auth_configuration
    application.router.add_get("/", ready)
    application.router.add_post(
        "/api/messages", jwt_authorization_decorator(messages)
    )
    application.on_cleanup.append(cleanup)
    return application


def start_server() -> None:
    web.run_app(
        create_web_app(),
        host=environ.get("HOST", "localhost"),
        port=int(environ.get("PORT", "3978")),
    )

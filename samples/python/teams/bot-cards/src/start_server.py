# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Serve the sample's public pages and authenticated Teams messages."""

from os import environ
from pathlib import Path

from aiohttp import web
from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_decorator,
    start_agent_process,
)
from microsoft_agents.hosting.core.authorization import AuthTypes

from .agent import create_agent


async def readiness(_request: web.Request) -> web.Response:
    return web.Response(text="Agent Cards is ready.")


async def messages_unconfigured(_request: web.Request) -> web.Response:
    raise web.HTTPServiceUnavailable(text="Agent connection settings are not configured.")


def create_web_app() -> web.Application:
    app = web.Application()
    app.router.add_get("/", readiness)

    configuration = load_configuration_from_env(environ)
    connections = configuration.get("CONNECTIONS", {})
    has_connection_settings = any(
        any(connection.get("SETTINGS", {}).values())
        for connection in connections.values()
    )
    if not has_connection_settings:
        # Readiness is available before credentials are provisioned.
        app.router.add_post("/api/messages", messages_unconfigured)
        return app

    connection_manager = MsalConnectionManager(**configuration)
    auth_configuration = connection_manager.get_default_connection_configuration()
    if auth_configuration.AUTH_TYPE == AuthTypes.client_secret and not (
        auth_configuration.client_id
        and auth_configuration.CLIENT_SECRET
        and auth_configuration.tenant_id
    ):
        raise ValueError("Configure CLIENTID, CLIENTSECRET and TENANTID for SERVICE_CONNECTION.")

    adapter = CloudAdapter(connection_manager=connection_manager)
    agent = create_agent(configuration, connection_manager)

    @jwt_authorization_decorator
    async def messages(request: web.Request) -> web.StreamResponse:
        return await start_agent_process(request, agent, adapter)

    app["agent_configuration"] = auth_configuration
    app.router.add_post("/api/messages", messages)
    return app


def start_server() -> None:
    load_dotenv(Path(__file__).resolve().parents[1] / ".env")
    web.run_app(
        create_web_app(),
        host="0.0.0.0",
        port=int(environ.get("PORT", "3978")),
    )

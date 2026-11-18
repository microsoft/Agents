# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ

from aiohttp import web
from aiohttp.web import Application, Request, Response, run_app
from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)

from src.agent import create_agent


@web.middleware
async def readiness_and_jwt_middleware(
    request: Request,
    handler,
):
    if request.method == "GET" and request.path == "/":
        return web.Response(text="Wikipedia Search agent is ready.")

    connection_manager = request.app.get("connection_manager")
    if connection_manager is None:
        load_dotenv()
        configuration = load_configuration_from_env(environ)
        connection_manager = MsalConnectionManager(**configuration)
        request.app["connection_manager"] = connection_manager
        request.app["agent_configuration"] = (
            connection_manager.get_default_connection_configuration()
        )

    return await jwt_authorization_middleware(request, handler)


def create_web_app() -> Application:
    app = Application(middlewares=[readiness_and_jwt_middleware])

    async def process_message(request: Request) -> Response:
        agent = request.app.get("agent_app")
        if agent is None:
            agent = create_agent(
                {"connection_manager": request.app["connection_manager"]}
            )
            request.app["agent_app"] = agent
            request.app["adapter"] = agent.adapter
        adapter: CloudAdapter = request.app["adapter"]
        return await start_agent_process(request, agent, adapter)

    app.router.add_get("/", lambda _request: web.Response(text="Wikipedia Search agent is ready."))
    app.router.add_post("/api/messages", process_message)
    return app


def run_server() -> None:
    run_app(
        create_web_app(),
        host="0.0.0.0",
        port=int(environ.get("PORT", "3978")),
    )

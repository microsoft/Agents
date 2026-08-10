# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ

from aiohttp.web import Application, Request, Response, run_app
from microsoft_agents.authentication.entra_auth_sidecar import SidecarAuth
from microsoft_agents.hosting.aiohttp import (
    CloudAdapter,
    jwt_authorization_middleware,
    start_agent_process,
)
from microsoft_agents.hosting.core import AgentApplication, AgentAuthConfiguration

def start_server(
    agent_application: AgentApplication,
    auth_configuration: AgentAuthConfiguration,
    sidecar_auth: SidecarAuth,
) -> None:
    async def entry_point(req: Request) -> Response:
        return await start_agent_process(
            req,
            req.app["agent_app"],
            req.app["adapter"],
        )

    async def client_lifecycle(app: Application):
        yield
        await app["sidecar_auth"].close()

    app = Application(middlewares=[jwt_authorization_middleware])
    app.router.add_post("/api/messages", entry_point)
    app["agent_configuration"] = auth_configuration
    app["agent_app"] = agent_application
    app["adapter"] = agent_application.adapter
    app["sidecar_auth"] = sidecar_auth
    app.cleanup_ctx.append(client_lifecycle)

    run_app(
        app,
        host=environ.get("HOST", "127.0.0.1"),
        port=int(environ.get("PORT", "3978")),
    )

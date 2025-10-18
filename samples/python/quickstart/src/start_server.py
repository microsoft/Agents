from os import environ
from microsoft_agents.hosting.core import AgentApplication, AgentAuthConfiguration
from microsoft_agents.hosting.aiohttp import (
    start_agent_process,
    jwt_authorization_middleware,
    CloudAdapter,
)
from aiohttp.web import Request, Response, Application, run_app


def start_server(
    agent_application: AgentApplication, auth_configuration: AgentAuthConfiguration
):
    async def entry_point(req: Request) -> Response:
        agent: AgentApplication = req.app["agent_app"]
        adapter: CloudAdapter = req.app["adapter"]
        return await start_agent_process(
            req,
            agent,
            adapter,
        )

    async def liveness_check(req: Request) -> Response:
        return Response(text="OK", status=200)

    async def readiness_check(req: Request) -> Response:
        return Response(text="OK", status=200)

    APP = Application(middlewares=[jwt_authorization_middleware])
    APP.router.add_post("/api/messages", entry_point)
    APP.router.add_get("/liveness", liveness_check)
    APP.router.add_get("/readiness", readiness_check)
    APP["agent_configuration"] = auth_configuration
    APP["agent_app"] = agent_application
    APP["adapter"] = agent_application.adapter

    try:
        run_app(APP, host="0.0.0.0", port=environ.get("PORT", 8088))
    except Exception as error:
        raise error

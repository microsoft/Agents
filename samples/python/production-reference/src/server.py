# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
import json
import time
from urllib.parse import urlparse
from uuid import uuid4

from aiohttp import web
from microsoft_agents.hosting.aiohttp import jwt_authorization_middleware
from microsoft_agents.hosting.core.state.agent_state import CachedAgentState


PAYLOAD_LIMIT = 256 * 1024


def allowed_service_url(value, production):
    if not isinstance(value, str):
        return False
    try:
        parsed = urlparse(value)
        if parsed.username or parsed.password or parsed.query or parsed.fragment:
            return False
        if parsed.hostname == "webchat.botframework.com":
            return parsed.scheme == "https" and parsed.port in {None, 443}
        return (
            not production
            and parsed.scheme in {"http", "https"}
            and parsed.hostname
            in {
                "localhost",
                "127.0.0.1",
                "::1",
            }
        )
    except ValueError:
        return False


def create_server(config, storage, agent, auth, telemetry, cleanup=None):
    @web.middleware
    async def safe_errors(request, handler):
        started = time.monotonic()
        status = 500
        try:
            response = await handler(request)
            status = response.status
            if response.status >= 400 and request.path == "/api/messages":
                return web.json_response(
                    {"error": "Request could not be processed."}, status=response.status
                )
            return response
        except web.HTTPException as error:
            status = error.status
            telemetry.failure("payload" if error.status < 500 else "http")
            return web.json_response(
                {"error": "Request could not be processed."}, status=error.status
            )
        except Exception:
            telemetry.failure("http")
            return web.json_response({"error": "Request could not be processed."}, status=500)
        finally:
            telemetry.request(status, time.monotonic() - started)

    @web.middleware
    async def message_security(request, handler):
        if request.path != "/api/messages":
            return await handler(request)
        # Bound chunked bodies too, before JWT validation and SDK processing.
        try:
            activity = await request.json()
        except (json.JSONDecodeError, UnicodeDecodeError):
            raise web.HTTPBadRequest()
        if not isinstance(activity, dict):
            raise web.HTTPBadRequest()

        async def authorized(req):
            if not allowed_service_url(activity.get("serviceUrl"), config.production):
                raise web.HTTPBadRequest()
            if config.production and activity.get("channelId") != "webchat":
                raise web.HTTPBadRequest()
            claims = req["claims_identity"].claims
            if config.production:
                # SDK validation also trusts first-party Entra issuers. This
                # reference permits only Bot Service, using verified claims.
                if claims.get("iss") != "https://api.botframework.com":
                    raise web.HTTPUnauthorized()
                expiry = claims.get("exp")
                if (
                    isinstance(expiry, bool)
                    or not isinstance(expiry, (int, float))
                    or expiry <= time.time()
                ):
                    raise web.HTTPUnauthorized()
            claim_url = claims.get("serviceurl")
            if claim_url and claim_url != activity.get("serviceUrl"):
                raise web.HTTPUnauthorized()
            return await handler(req)

        return await jwt_authorization_middleware(request, authorized)

    app = web.Application(
        client_max_size=PAYLOAD_LIMIT, middlewares=[safe_errors, message_security]
    )
    app["agent_configuration"] = auth

    async def live(request):
        return web.json_response({"status": "ok"})

    async def ready(request):
        key = f"health.readiness.{uuid4()}"
        try:
            async with asyncio.timeout(5):
                await storage.write({key: CachedAgentState(state={"checked": True})})
                await storage.delete([key])
            return web.json_response({"status": "ready"})
        except Exception:
            telemetry.failure("readiness")
            return web.json_response({"status": "not ready"}, status=503)

    async def messages(request):
        return await agent.adapter.process(request, agent)

    app.router.add_get("/health/live", live)
    app.router.add_get("/health/ready", ready)
    app.router.add_post("/api/messages", messages)
    if cleanup:
        app.on_cleanup.append(cleanup)
    return app

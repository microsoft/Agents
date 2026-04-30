# Copyright 2025 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""
A2A Entry Point - Exposes the Brand Search Optimization Agent via Agent-to-Agent Protocol.

This file wraps the existing agent with the A2A adapter, automatically creating:
- Agent Card at .well-known/agent-card.json (for agent discovery)
- REST API endpoints (for Copilot Studio and other A2A clients)
- Session management (for multi-turn conversations)

Usage:
    python run_a2a.py

This will start a server on http://localhost:8080 that exposes the agent
via the Agent-to-Agent protocol, making it discoverable by Copilot Studio
and other A2A-compatible clients.
"""

import os
import logging
import httpx
import uvicorn
from dotenv import load_dotenv
from starlette.middleware.cors import CORSMiddleware
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.applications import Starlette

from a2a.types import AgentCapabilities
from a2a.server.apps import A2AStarletteApplication
from a2a.server.request_handlers import DefaultRequestHandler
from a2a.server.tasks import InMemoryTaskStore, InMemoryPushNotificationConfigStore, BasePushNotificationSender

from google.adk.a2a.executor.a2a_agent_executor import A2aAgentExecutor
from google.adk.a2a.utils.agent_card_builder import AgentCardBuilder
from google.adk.runners import Runner
from google.adk.artifacts import InMemoryArtifactService
from google.adk.sessions import InMemorySessionService
from google.adk.memory import InMemoryMemoryService
from google.adk.auth.credential_service.in_memory_credential_service import InMemoryCredentialService

from brand_search_optimization.agent import root_agent

# Load .env file
load_dotenv()

# Enable verbose logging
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger("a2a_server")


class RequestLoggingMiddleware(BaseHTTPMiddleware):
    """Log all incoming requests with details for debugging."""
    async def dispatch(self, request: Request, call_next):
        body = b""
        if request.method in ("POST", "PUT", "PATCH"):
            body = await request.body()
        logger.info(
            f"[REQ] {request.method} {request.url.path} "
            f"from {request.client.host if request.client else 'unknown'} "
            f"Content-Type: {request.headers.get('content-type', 'N/A')}"
        )
        if body:
            # Truncate large bodies for readability
            body_str = body.decode("utf-8", errors="replace")[:2000]
            logger.info(f"[BODY] Request body: {body_str}")
        response = await call_next(request)
        logger.info(f"[RSP] Response: {response.status_code} for {request.method} {request.url.path}")
        return response

# A2A agent card URL configuration
# When using Dev Tunnel or a public deployment, set these so the agent card
# advertises the correct reachable URL to Copilot Studio.
a2a_host = os.getenv("A2A_HOST", "localhost")
a2a_port = int(os.getenv("A2A_PORT", "8080"))
a2a_protocol = os.getenv("A2A_PROTOCOL", "http")
rpc_url = f"{a2a_protocol}://{a2a_host}:{a2a_port}/"

# --- Build A2A components with SSE streaming + push notifications enabled ---

# 1. Runner factory — creates ADK runner with in-memory services
async def create_runner() -> Runner:
    return Runner(
        app_name=root_agent.name or "adk_agent",
        agent=root_agent,
        artifact_service=InMemoryArtifactService(),
        session_service=InMemorySessionService(),
        memory_service=InMemoryMemoryService(),
        credential_service=InMemoryCredentialService(),
    )

# 2. Task store & push notification config store (in-memory)
task_store = InMemoryTaskStore()
push_config_store = InMemoryPushNotificationConfigStore()

# 3. Agent executor (bridges ADK agent ↔ A2A protocol)
agent_executor = A2aAgentExecutor(runner=create_runner)

# 3b. Push notification sender — POSTs task updates to registered webhook URLs
httpx_client = httpx.AsyncClient(timeout=30.0)
push_sender = BasePushNotificationSender(
    httpx_client=httpx_client,
    config_store=push_config_store,
)

# 4. Request handler — handles message/send, message/stream, push config RPCs
request_handler = DefaultRequestHandler(
    agent_executor=agent_executor,
    task_store=task_store,
    push_config_store=push_config_store,
    push_sender=push_sender,
)

# 5. Agent card with capabilities — SSE streaming + push notifications enabled
agent_card_builder = AgentCardBuilder(
    agent=root_agent,
    rpc_url=rpc_url,
    capabilities=AgentCapabilities(
        streaming=True,               # Enable SSE via message/stream
        push_notifications=True,       # Enable push notifications
        state_transition_history=True, # Expose task state change history
    ),
)

# 6. Build Starlette app with A2A routes
app = Starlette()

async def setup_a2a():
    """Build agent card and register A2A routes on startup."""
    agent_card = await agent_card_builder.build()
    logger.info(f"[OK] Agent card built: capabilities={agent_card.capabilities}")
    a2a_app = A2AStarletteApplication(
        agent_card=agent_card,
        http_handler=request_handler,
    )
    a2a_app.add_routes_to_app(app)

app.add_event_handler("startup", setup_a2a)

# Add CORS middleware so Copilot Studio's browser frontend can fetch the agent card
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# Add request logging middleware for debugging
app.add_middleware(RequestLoggingMiddleware)

if __name__ == "__main__":
    # Start the A2A server
    # - Serves agent card at /.well-known/agent-card.json
    # - Exposes REST endpoints for A2A protocol
    # - Handles session management automatically
    port = int(os.getenv("PORT", "8080"))
    print(f"\n[*] Starting A2A Server on http://localhost:{port}")
    print(f"[i] Agent Card available at: http://localhost:{port}/.well-known/agent-card.json")
    print(f"[i] Agent Card URL advertised: {a2a_protocol}://{a2a_host}:{a2a_port}")
    print(f"[>] Use this URL in Copilot Studio to connect to this agent\n")

    uvicorn.run(app, host="0.0.0.0", port=port)

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Entry point for the Brand Intelligence Advisor agent.

Usage:
    python run_server.py
    python -m brand_intelligence_advisor  (if __main__.py added)

Starts the aiohttp server with M365 SDK messaging and A2A webhook endpoints.
"""

import logging

# ── Logging Setup ─────────────────────────────────────────────────────────────
# Configure loggers for each component so we can control verbosity per module.

logging.basicConfig(level=logging.INFO, format="%(name)s %(levelname)s: %(message)s")

# M365 Agents SDK logging
ms_agents_logger = logging.getLogger("microsoft_agents")
ms_agents_logger.setLevel(logging.INFO)

# A2A client logging (DEBUG shows full JSON-RPC payloads)
a2a_logger = logging.getLogger("brand_intelligence_advisor.tools.a2a_client")
a2a_logger.setLevel(logging.DEBUG)

# Brand advisor logging
advisor_logger = logging.getLogger("brand_intelligence_advisor.tools.brand_advisor")
advisor_logger.setLevel(logging.INFO)

# Server logging
server_logger = logging.getLogger("brand_intelligence_advisor.server")
server_logger.setLevel(logging.INFO)

# Agent logging
agent_logger = logging.getLogger("brand_intelligence_advisor.agent")
agent_logger.setLevel(logging.INFO)

# Orchestrator logging
orchestrator_logger = logging.getLogger("brand_intelligence_advisor.orchestrator")
orchestrator_logger.setLevel(logging.INFO)

# Azure SDKs logging (reduce noise from Azure HTTP pipeline)
azure_logger = logging.getLogger("azure")
azure_logger.setLevel(logging.WARNING)

# ── Import & Start ────────────────────────────────────────────────────────────

from brand_intelligence_advisor.agent import (  # noqa: E402
    AGENT_APP,
    CONNECTION_MANAGER,
    push_notifications,
    LLM_AVAILABLE,
)
from brand_intelligence_advisor.server import start_server  # noqa: E402

llm_status = (
    "ENABLED (Semantic Kernel + Azure OpenAI)"
    if LLM_AVAILABLE
    else "DISABLED (regex fallback)"
)

print(
    f"""
+------------------------------------------------------------------+
|  Brand Intelligence Advisor                                      |
|  M365 Agents SDK + Semantic Kernel + A2A Protocol                |
|                                                                  |
|  LLM Orchestration: {llm_status:<44}|
|                                                                  |
|  A2A Patterns:                                                   |
|    a. Ping  (message/send)     -- Synchronous blocking           |
|    b. SSE   (message/stream)   -- Server-Sent Events             |
|    c. Push  (webhook notify)   -- Async with callback            |
|    d. Status (webhook receive) -- View push notifications        |
|                                                                  |
+------------------------------------------------------------------+
"""
)

start_server(
    agent_application=AGENT_APP,
    auth_configuration=CONNECTION_MANAGER.get_default_connection_configuration(),
    push_notifications=push_notifications,
)

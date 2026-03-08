# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Brand Intelligence Advisor — M365 Agents SDK Agent.

This is the main agent module that bridges the M365 Agents SDK messaging
layer with the Semantic Kernel orchestrator and A2A protocol client.

Architecture:
    M365 Agents SDK (messaging from Teams / WebChat / CLI)
      → AgentApplication message handler
        → AgentOrchestrator (Semantic Kernel + Azure OpenAI)
          → BrandToolsPlugin → A2A Client → Google ADK Agent
        ← Strategic analysis response
      ← M365 SDK response to user

The LLM decides which A2A pattern to use based on user intent:
    - ping  (message/send)       — quick synchronous analysis
    - stream (message/stream)    — real-time SSE streaming
    - push  (message/send + wh)  — background with webhook notification

When Azure AI Foundry is not configured, falls back to regex-based
command routing for basic functionality.
"""

import sys
import logging
import traceback
from os import environ

from dotenv import load_dotenv

from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.activity import load_configuration_from_env

from .tools.a2a_client import A2AClient
from .tools.brand_advisor import BrandAdvisor

logger = logging.getLogger(__name__)

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)


# ---------------------------------------------------------------------------
# M365 Agents SDK Infrastructure
# ---------------------------------------------------------------------------

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE,
    adapter=ADAPTER,
    authorization=AUTHORIZATION,
    **agents_sdk_config,
)


# ---------------------------------------------------------------------------
# A2A Client + Advisor + Orchestrator Setup
# ---------------------------------------------------------------------------

A2A_AGENT_URL = environ.get("A2A_AGENT_URL", "http://localhost:8080")
AGENT_HOST = environ.get("AGENT_HOST", "localhost")
AGENT_PORT = int(environ.get("AGENT_PORT", "3978"))

a2a_client = A2AClient(base_url=A2A_AGENT_URL)
advisor = BrandAdvisor()

# In-memory store for push notifications (populated by webhook in server.py)
push_notifications: list[dict] = []

# Webhook URL for this agent (ADK agent POSTs results here)
webhook_url = f"http://{AGENT_HOST}:{AGENT_PORT}/a2a/webhook"

# Try to initialize the Semantic Kernel orchestrator
orchestrator = None
LLM_AVAILABLE = False

try:
    from .orchestrator import AgentOrchestrator

    orchestrator = AgentOrchestrator(
        a2a_client=a2a_client,
        advisor=advisor,
        push_notifications=push_notifications,
        webhook_url=webhook_url,
    )
    LLM_AVAILABLE = True
    logger.info("Semantic Kernel orchestrator initialized successfully")
except Exception as e:
    logger.warning(
        f"LLM orchestration not available: {e} -- "
        f"Falling back to regex-based command routing. "
        f"Set AZURE_AI_FOUNDRY_ENDPOINT in .env to enable LLM orchestration."
    )


# ---------------------------------------------------------------------------
# Welcome Handler
# ---------------------------------------------------------------------------

@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    """Send welcome message when a user joins."""
    try:
        card = await a2a_client.discover()
        agent_name = card.name
        logger.info(f"Connected to A2A agent: {agent_name}")
    except Exception as e:
        agent_name = "Brand Search Optimization"
        logger.warning(f"Could not discover A2A agent: {e}")

    if LLM_AVAILABLE:
        welcome = (
            f"# Brand Intelligence Advisor\n\n"
            f"I'm your AI-powered brand intelligence advisor, connected to the "
            f"**{agent_name}** agent via the A2A protocol.\n\n"
            f"Just tell me what you need in natural language:\n\n"
            f"- *\"How is Nike performing in shoes?\"*\n"
            f"- *\"Compare Nike and Adidas in sportswear\"*\n"
            f"- *\"Run a deep analysis on Puma Active\"*\n"
            f"- *\"What does CTR mean?\"*\n\n"
            f"I'll choose the best A2A communication pattern automatically "
            f"and provide strategic insights on top of the raw data.\n\n"
            f"*Powered by M365 Agents SDK + Semantic Kernel + A2A Protocol*"
        )
    else:
        welcome = advisor.get_help_text(agent_name)

    await context.send_activity(welcome)
    return True


# ---------------------------------------------------------------------------
# Main Message Handler
# ---------------------------------------------------------------------------

@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    """
    All user messages flow through this single handler.

    LLM mode:  SK orchestrator reasons about intent → calls tools → synthesizes
    Fallback:  Regex-based command routing (ping/stream/push/status/etc.)
    """
    text = (context.activity.text or "").strip()
    if not text:
        return

    if LLM_AVAILABLE:
        await _handle_llm(context, text)
    else:
        await _handle_fallback(context, text)


async def _handle_llm(context: TurnContext, text: str):
    """Route message through the Semantic Kernel orchestrator."""
    conversation_id = context.activity.conversation.id or "default"
    await context.send_activity({"type": "typing"})

    try:
        response = await orchestrator.process_message(text, conversation_id)
        await context.send_activity(response)
    except Exception as e:
        logger.error(f"Orchestrator error: {e}")
        traceback.print_exc()
        await context.send_activity(
            f"I encountered an error processing your request: {str(e)}\n\n"
            f"Please try again."
        )


async def _handle_fallback(context: TurnContext, text: str):
    """
    Regex-based fallback when LLM orchestration is not available.
    Supports all 3 A2A patterns + local capabilities via command prefixes.
    """
    text_lower = text.lower().strip()

    # ── A2A Pattern: Ping (message/send) ──────────────────────────────
    if text_lower.startswith("ping "):
        raw = text[5:].strip()
        query = advisor.parse_query(raw)
        if not query.is_valid:
            await context.send_activity(f"Error: {query.error}")
            return
        try:
            a2a_request = advisor.formulate_a2a_request(query)
            task = await a2a_client.send_message(a2a_request)
            response_text = task.message or f"Task completed: {task.status}"
            formatted = advisor.format_executive_summary(
                query.brand, response_text, "ping"
            )
            advisor.record_analysis(query, "ping", response_text, task.message)
            await context.send_activity(formatted)
        except Exception as e:
            await context.send_activity(
                f"Ping failed: {e}\n\n"
                f"Make sure the ADK agent is running at `{A2A_AGENT_URL}`"
            )

    # ── A2A Pattern: Stream (message/stream) ──────────────────────────
    elif text_lower.startswith("stream "):
        raw = text[7:].strip()
        query = advisor.parse_query(raw)
        if not query.is_valid:
            await context.send_activity(f"Error: {query.error}")
            return
        try:
            a2a_request = advisor.formulate_a2a_request(query)
            collected = []
            async for event in a2a_client.stream_message(a2a_request):
                if event.text:
                    collected.append(event.text)
            full_response = "\n".join(collected)
            advisor.record_analysis(query, "stream", full_response)
            formatted = advisor.format_executive_summary(
                query.brand, full_response, "sse"
            )
            await context.send_activity(formatted)
        except Exception as e:
            await context.send_activity(f"Stream failed: {e}")

    # ── A2A Pattern: Push (message/send + webhook) ────────────────────
    elif text_lower.startswith("push "):
        raw = text[5:].strip()
        query = advisor.parse_query(raw)
        if not query.is_valid:
            await context.send_activity(f"Error: {query.error}")
            return
        try:
            a2a_request = advisor.formulate_a2a_request(query)
            task = await a2a_client.send_with_push(a2a_request, webhook_url)
            formatted = advisor.format_push_acknowledgment(
                query.brand, task.task_id
            )
            advisor.record_analysis(
                query, "push", f"Task submitted: {task.status}"
            )
            await context.send_activity(formatted)
        except Exception as e:
            await context.send_activity(f"Push failed: {e}")

    # ── Status (check push notifications) ─────────────────────────────
    elif text_lower == "status":
        if not push_notifications:
            await context.send_activity(
                "No push notifications received yet. Use `push <brand>` first."
            )
        else:
            lines = ["**Received Push Notifications**\n"]
            for i, n in enumerate(push_notifications, 1):
                tid = n.get("task_id", "?")[:12]
                st = n.get("status", "?")
                lines.append(f"  {i}. Task `{tid}...` -- **{st}**")
            await context.send_activity("\n".join(lines))

    # ── Local capabilities (no A2A needed) ────────────────────────────
    elif text_lower == "help":
        await context.send_activity(advisor.get_help_text())
    elif text_lower == "history":
        await context.send_activity(advisor.get_history_summary())
    elif text_lower == "strategy":
        await context.send_activity(advisor.get_strategy_tips())
    elif text_lower == "glossary":
        await context.send_activity(advisor.get_glossary())
    elif text_lower.startswith("define "):
        term = text[7:].strip()
        defn = advisor.get_seo_definition(term)
        await context.send_activity(
            defn or f"Term '{term}' not found. Type `glossary` for all terms."
        )
    else:
        await context.send_activity(
            "LLM orchestration is not configured. "
            "Set `AZURE_AI_FOUNDRY_ENDPOINT` in your `.env` file.\n\n"
            "Available commands: `ping <brand>`, `stream <brand>`, "
            "`push <brand>`, `status`, `help`, `glossary`, `define <term>`"
        )


# ---------------------------------------------------------------------------
# Error Handler
# ---------------------------------------------------------------------------

@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    """Global error handler for unhandled exceptions."""
    print(f"\n[on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()
    await context.send_activity(
        f"An unexpected error occurred: {str(error)}\nPlease try again."
    )

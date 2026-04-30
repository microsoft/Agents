# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Semantic Kernel Orchestrator for the Brand Intelligence Advisor.

Architecture:
    User message
      → M365 Agents SDK (agent.py)
        → AgentOrchestrator.process_message()
          → SK ChatCompletionAgent (automatic tool calling + LLM reasoning)
            → BrandToolsPlugin (@kernel_function methods)
              → A2A Client → Google ADK Agent (remote analysis)
            ← Tool result (JSON)
          ← LLM-synthesized strategic response (markdown)
        ← M365 SDK response
      ← User

What Semantic Kernel handles automatically:
    - LLM chat completions loop (no manual while-loop)
    - Function/tool calling dispatch (no manual JSON parsing)
    - Tool schema generation from @kernel_function decorators
    - Conversation history management

What this file contains:
    - BrandToolsPlugin: SK Plugin with 4 tools exposed via @kernel_function
    - AgentOrchestrator: Creates and invokes the ChatCompletionAgent
"""

import json
import logging
from os import environ
from typing import Annotated

import semantic_kernel as sk
from semantic_kernel.agents import ChatCompletionAgent
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from semantic_kernel.contents import ChatHistory
from semantic_kernel.functions import kernel_function

from .prompt import SYSTEM_PROMPT
from .tools.a2a_client import A2AClient
from .tools.brand_advisor import BrandAdvisor

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Semantic Kernel Plugin — replaces manual TOOL_DEFINITIONS + ToolExecutor
# ---------------------------------------------------------------------------


class BrandToolsPlugin:
    """
    SK Plugin exposing brand analysis tools as @kernel_function methods.

    Semantic Kernel auto-generates tool schemas from the type annotations
    and dispatches calls automatically — no manual JSON parsing needed.

    Tools:
        analyze_brand           — Call remote ADK agent via A2A (ping/stream/push)
        check_push_notifications — View webhook results from background jobs
        get_analysis_history    — Session analysis history for comparisons
        get_seo_glossary        — SEO terminology definitions
    """

    def __init__(
        self,
        a2a_client: A2AClient,
        advisor: BrandAdvisor,
        push_notifications: list[dict],
        webhook_url: str,
    ):
        self.a2a_client = a2a_client
        self.advisor = advisor
        self.push_notifications = push_notifications
        self.webhook_url = webhook_url

    # ── Tool 1: Brand Analysis via A2A ────────────────────────────────────

    @kernel_function(
        name="analyze_brand",
        description=(
            "Analyze a brand's search optimization performance by calling "
            "the remote A2A Brand Search Optimization agent. Choose the "
            "appropriate communication mode based on the user's needs. "
            "Returns structured JSON with the analysis results."
        ),
    )
    async def analyze_brand(
        self,
        brand: Annotated[str, "The brand name to analyze (e.g., Nike, Adidas, Puma)"],
        mode: Annotated[str, "A2A pattern: 'ping' (synchronous), 'stream' (SSE), 'push' (background webhook)"],
        category: Annotated[str, "Product category (e.g., Active, Tops & Tees). Map shoes/sportswear to Active. Use empty string if unknown."] = "",
    ) -> str:
        """Execute a brand analysis via the appropriate A2A pattern."""
        query = self.advisor.parse_query(
            f"{brand} {category}".strip() if category else brand
        )
        if not query.is_valid:
            return json.dumps({"error": query.error})

        a2a_request = self.advisor.formulate_a2a_request(query)

        # ── Ping: synchronous message/send ────────────────────────────
        if mode == "ping":
            task = await self.a2a_client.send_message(a2a_request)
            response_text = task.message or f"Task completed with status: {task.status}"
            self.advisor.record_analysis(query, "ping", response_text, task.message)
            return json.dumps({
                "pattern": "ping (message/send)",
                "brand": query.brand,
                "category": query.category,
                "task_id": task.task_id,
                "status": task.status,
                "analysis": response_text,
            })

        # ── Stream: SSE message/stream ────────────────────────────────
        elif mode == "stream":
            collected_chunks = []
            chunk_count = 0
            async for event in self.a2a_client.stream_message(a2a_request):
                chunk_count += 1
                if event.text:
                    collected_chunks.append(event.text)

            full_response = "\n".join(collected_chunks)
            self.advisor.record_analysis(query, "stream", full_response)
            return json.dumps({
                "pattern": "stream (message/stream SSE)",
                "brand": query.brand,
                "category": query.category,
                "chunks_received": chunk_count,
                "analysis": full_response,
            })

        # ── Push: background with webhook notification ────────────────
        elif mode == "push":
            task = await self.a2a_client.send_with_push(a2a_request, self.webhook_url)
            self.advisor.record_analysis(query, "push", f"Task submitted: {task.status}")
            return json.dumps({
                "pattern": "push (message/send + webhook)",
                "brand": query.brand,
                "category": query.category,
                "task_id": task.task_id,
                "status": task.status,
                "webhook_url": self.webhook_url,
                "note": (
                    "Analysis is running in the background. "
                    "Use check_push_notifications to see results when ready."
                ),
            })

        else:
            return json.dumps({"error": f"Unknown mode: {mode}"})

    # ── Tool 2: Check Push Notifications ──────────────────────────────────

    @kernel_function(
        name="check_push_notifications",
        description=(
            "Check the status of background (push) analyses. Returns any "
            "webhook notifications received from the remote A2A agent."
        ),
    )
    async def check_push_notifications(self) -> str:
        """Return all received push notifications."""
        if not self.push_notifications:
            return json.dumps({
                "notifications": [],
                "message": (
                    "No push notifications received yet. "
                    "Submit a brand analysis with mode='push' first."
                ),
            })

        return json.dumps({
            "notifications": [
                {
                    "task_id": n.get("task_id", "unknown"),
                    "status": n.get("status", "unknown"),
                    "received_at": n.get("received_at", "unknown"),
                    "text": n.get("text", "")[:500],
                }
                for n in self.push_notifications
            ],
            "count": len(self.push_notifications),
        })

    # ── Tool 3: Analysis History ──────────────────────────────────────────

    @kernel_function(
        name="get_analysis_history",
        description=(
            "Retrieve past brand analyses from the current session. "
            "Use this for comparisons, trend analysis across the session, "
            "or when the user asks about previous results."
        ),
    )
    async def get_analysis_history(self) -> str:
        """Return session analysis history."""
        history = self.advisor.get_history_data()
        if not history:
            return json.dumps({
                "history": [],
                "message": "No analyses performed yet in this session.",
            })
        return json.dumps({"history": history, "count": len(history)})

    # ── Tool 4: SEO Glossary ─────────────────────────────────────────────

    @kernel_function(
        name="get_seo_glossary",
        description=(
            "Look up SEO and brand optimization terminology. "
            "Use when the user asks 'what is CTR?', 'define SERP', etc."
        ),
    )
    async def get_seo_glossary(
        self,
        term: Annotated[str, "Specific term to define (e.g., 'CTR', 'SERP'). Use empty string for full glossary."] = "",
    ) -> str:
        """Return SEO glossary data or a specific term definition."""
        if term and term.strip():
            definition = self.advisor.get_seo_definition_raw(term.strip())
            if definition:
                return json.dumps({"term": term, "definition": definition})
            else:
                return json.dumps({
                    "error": f"Term '{term}' not found in glossary",
                    "available_terms": self.advisor.get_glossary_terms(),
                })
        else:
            return json.dumps({"glossary": self.advisor.get_glossary_data()})


# ---------------------------------------------------------------------------
# Agent Orchestrator — public interface for agent.py, test_demo.py, etc.
# ---------------------------------------------------------------------------


class AgentOrchestrator:
    """
    LLM Orchestrator using Semantic Kernel + Azure OpenAI.

    Replaces the manual Chat Completions + function-calling loop with
    Semantic Kernel's ChatCompletionAgent, which handles tool schema
    generation, automatic function dispatch, and the LLM ↔ tool loop.

    Public interface:
        __init__(a2a_client, advisor, push_notifications, webhook_url)
        process_message(user_text, conversation_id) -> str
    """

    def __init__(
        self,
        a2a_client: A2AClient,
        advisor: BrandAdvisor,
        push_notifications: list[dict],
        webhook_url: str,
    ):
        # ── Read Azure OpenAI config from environment ─────────────────
        endpoint = environ.get("AZURE_AI_FOUNDRY_ENDPOINT", "")
        api_key = environ.get("AZURE_AI_FOUNDRY_API_KEY", "")

        if not endpoint:
            raise ValueError(
                "AZURE_AI_FOUNDRY_ENDPOINT environment variable is required. "
                "Set it to your Azure AI Services endpoint "
                "(e.g., https://your-resource.services.ai.azure.com)."
            )
        if not api_key:
            raise ValueError(
                "AZURE_AI_FOUNDRY_API_KEY environment variable is required. "
                "Set it to your Azure AI Services API key."
            )

        model = environ.get("AZURE_AI_FOUNDRY_MODEL", "gpt-4o-mini")

        # ── 1. Create the Kernel ──────────────────────────────────────
        self.kernel = sk.Kernel()

        # ── 2. Create Azure OpenAI chat completion service ────────────
        self.chat_service = AzureChatCompletion(
            service_id="brand-advisor",
            deployment_name=model,
            endpoint=endpoint,
            api_key=api_key,
            api_version="2024-12-01-preview",
        )

        # ── 3. Register the plugin (tools auto-discovered) ───────────
        self.plugin = BrandToolsPlugin(
            a2a_client=a2a_client,
            advisor=advisor,
            push_notifications=push_notifications,
            webhook_url=webhook_url,
        )
        self.kernel.add_plugin(self.plugin, plugin_name="brand")

        # ── 4. Create the ChatCompletionAgent ─────────────────────────
        self.agent = ChatCompletionAgent(
            service=self.chat_service,
            kernel=self.kernel,
            name="BrandIntelligenceAdvisor",
            instructions=SYSTEM_PROMPT,
        )

        # ── 5. Per-conversation chat history ──────────────────────────
        self._histories: dict[str, ChatHistory] = {}

        logger.info(
            f"SK orchestrator ready: model={model}, "
            f"endpoint={endpoint[:60]}..., "
            f"plugin=brand (4 functions)"
        )

    def _get_history(self, conversation_id: str) -> ChatHistory:
        """Get or create a ChatHistory for the given conversation."""
        if conversation_id not in self._histories:
            self._histories[conversation_id] = ChatHistory()
        return self._histories[conversation_id]

    async def process_message(
        self, user_text: str, conversation_id: str
    ) -> str:
        """
        Process a user message through the SK ChatCompletionAgent.

        The agent automatically handles:
            1. Sending the message + history to the LLM
            2. Parsing any tool_calls in the response
            3. Executing the matching @kernel_function methods
            4. Feeding results back to the LLM
            5. Repeating until the LLM produces a final text response
        """
        history = self._get_history(conversation_id)
        history.add_user_message(user_text)

        logger.info(
            f"Processing message via SK agent "
            f"(conversation={conversation_id}, history={len(history)} messages)"
        )

        # The agent handles the entire LLM ↔ tool loop automatically
        response_parts = []
        async for message in self.agent.invoke(history):
            response_parts.append(str(message))

        final_text = "\n".join(response_parts) if response_parts else ""

        # Trim history to prevent token overflow (keep last 40 messages)
        if len(history) > 50:
            trimmed = ChatHistory()
            for msg in list(history)[-40:]:
                trimmed.add_message(msg)
            self._histories[conversation_id] = trimmed
            logger.info("Trimmed conversation history to 40 messages")

        return final_text

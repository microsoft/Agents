# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import annotations

import logging
from os import environ
from typing import Optional

from dotenv import load_dotenv
from semantic_kernel.connectors.ai.open_ai import (
    AzureChatCompletion,
    OpenAIChatCompletion,
)
from semantic_kernel.contents import ChatHistory

from microsoft_agents.activity import Attachment, load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    AgentApplication,
    Authorization,
    MemoryStorage,
    StoreItem,
    TurnContext,
    TurnState,
)

from .agent import AdaptiveCardWeatherForecastAgentResponse, WeatherForecastAgent
from .plugins.progress import reset_turn_context, set_turn_context

logger = logging.getLogger(__name__)

WELCOME_MESSAGE = "Hello and welcome! I'm here to help with all your weather forecast needs!"
PROCESSING_MESSAGE = "Working on a response for you"
FAILURE_MESSAGE = "Sorry, I couldn't get the weather forecast at the moment."

load_dotenv()


def _required_setting(name: str) -> str:
    value = environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"{name} environment variable is missing and required.")
    return value


def _create_chat_completion():
    use_azure_openai = environ.get("USE_AZURE_OPENAI", "true").lower() == "true"
    if use_azure_openai:
        return AzureChatCompletion(
            api_version=_required_setting("AZURE_OPENAI_API_VERSION"),
            endpoint=_required_setting("AZURE_OPENAI_ENDPOINT"),
            api_key=_required_setting("AZURE_OPENAI_API_KEY"),
            deployment_name=_required_setting("AZURE_OPENAI_DEPLOYMENT_NAME"),
        )

    return OpenAIChatCompletion(
        ai_model_id=_required_setting("OPENAI_MODEL_ID"),
        api_key=_required_setting("OPENAI_API_KEY"),
    )


agents_sdk_config = load_configuration_from_env(environ)
STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)
WEATHER_FORECAST_AGENT = WeatherForecastAgent(_create_chat_completion())

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


class ChatHistoryStoreItem(StoreItem):
    def __init__(self, chat_history: Optional[ChatHistory] = None):
        self.chat_history = chat_history or ChatHistory()

    def store_item_to_json(self) -> dict:
        return self.chat_history.model_dump()

    @staticmethod
    def from_json_to_store_item(json_data: dict) -> ChatHistoryStoreItem:
        return ChatHistoryStoreItem(ChatHistory.model_validate(json_data))


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    for member in context.activity.members_added:
        if member.id != context.activity.recipient.id:
            await context.send_activity(WELCOME_MESSAGE)


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, state: TurnState):
    context.streaming_response.set_feedback_loop(True)
    context.streaming_response.set_generated_by_ai_label(True)
    context.streaming_response.queue_informative_update(PROCESSING_MESSAGE)

    history_store = state.get_value(
        "ConversationState.chatHistory",
        lambda: ChatHistoryStoreItem(),
        target_cls=ChatHistoryStoreItem,
    )

    try:
        user_text = (context.activity.text or "").strip()
        if not user_text:
            context.streaming_response.queue_text_chunk(
                "Please enter a weather question."
            )
            return

        context_token = set_turn_context(context)
        try:
            response = await WEATHER_FORECAST_AGENT.invoke_agent(
                user_text, history_store.chat_history
            )
        finally:
            reset_turn_context(context_token)

        if isinstance(response, AdaptiveCardWeatherForecastAgentResponse):
            # The Python SDK requires text when ending a streaming response. Use a
            # user-facing caption instead of its "end stream response" fallback.
            context.streaming_response.queue_text_chunk(
                "Here is the weather forecast for you:"
            )
            context.streaming_response.set_attachments(
                [
                    Attachment(
                        content_type="application/vnd.microsoft.card.adaptive",
                        content=response.content,
                    )
                ]
            )
        else:
            context.streaming_response.queue_text_chunk(response.content)
    except Exception:
        logger.exception("Error during agent execution")
        context.streaming_response.queue_text_chunk(FAILURE_MESSAGE)
    finally:
        state.set_value("ConversationState.chatHistory", history_store)
        await context.streaming_response.end_stream()

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ
import logging

from dotenv import load_dotenv
from agent_framework import (
    Agent,
    AgentSession,
    CompactionProvider,
    InMemoryHistoryProvider,
    SlidingWindowStrategy,
)
from agent_framework.openai import OpenAIChatClient

from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
)
from microsoft_agents.activity import load_configuration_from_env

from .tools import get_date, get_current_weather, get_weather_forecast
from .tools.progress import reset_turn_context, set_turn_context

logger = logging.getLogger(__name__)

load_dotenv()


def _required_setting(name: str) -> str:
    value = environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"{name} environment variable is missing and required.")
    return value


azure_openai_endpoint = _required_setting("AZURE_OPENAI_ENDPOINT")
azure_openai_api_key = _required_setting("AZURE_OPENAI_API_KEY")
azure_openai_model = _required_setting("AZURE_OPENAI_MODEL")
_required_setting("OPEN_WEATHER_API_KEY")

agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

AGENT_INSTRUCTIONS = """
You are a friendly feline assistant that helps people find the current weather or a weather forecast for a given place.
You will always speak like a cat.
Location is a city name, 2 letter US state codes should be resolved to the full name of the United States State.
You may ask follow up questions until you have enough information to answer the customers question, but once you have the current weather or a forecast, make sure to format it nicely in text.

For current weather, use the get_current_weather tool. You should include the current temperature, low and high temperatures, wind speed, humidity, and a short description of the weather.
For forecasts, use the get_weather_forecast tool. You should report on the next 5 days, including the current day, and include the date, high and low temperatures, and a short description of the weather.
You should use the get_date tool to get the current date and time.

When responding, make sure to format the information in a way that is easy to read and understand, markdown is good, and always speak like a cat. Use emojis if it fits the response!
"""

HISTORY = InMemoryHistoryProvider(skip_excluded=True)
CONTEXT_COMPACTION = CompactionProvider(
    before_strategy=SlidingWindowStrategy(
        keep_last_groups=10,
        preserve_system=True,
    ),
    history_source_id=HISTORY.source_id,
)

WEATHER_AGENT = Agent(
    client=OpenAIChatClient(
        azure_endpoint=azure_openai_endpoint,
        api_key=azure_openai_api_key,
        model=azure_openai_model,
    ),
    name="Purrfect Weather Agent",
    instructions=AGENT_INSTRUCTIONS,
    tools=[get_date, get_current_weather, get_weather_forecast],
    context_providers=[HISTORY, CONTEXT_COMPACTION],
    default_options={"store": False},
)

WELCOME_MESSAGE = (
    "Hello! I'm your friendly weather cat assistant. 🐱 "
    "I can help you find the current weather or a weather forecast for any city. "
    "Just tell me the city name and, if you're in the US, the 2-letter state code. Meow!"
)


def _restore_session(value: object) -> AgentSession:
    if isinstance(value, dict):
        return AgentSession.from_dict(value)
    return WEATHER_AGENT.create_session()


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    members_added = context.activity.members_added
    for member in members_added:
        if member.id != context.activity.recipient.id:
            await context.send_activity(WELCOME_MESSAGE)


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, state: TurnState):
    user_text = (context.activity.text or "").strip()
    if not user_text:
        return

    session = None

    context.streaming_response.queue_informative_update("Just a moment please..")

    try:
        stored_session = state.get_value(
            "ConversationState.agentSession", lambda: None
        )
        session = _restore_session(stored_session)

        context_token = set_turn_context(context)
        try:
            async for chunk in WEATHER_AGENT.run(
                user_text, session=session, stream=True
            ):
                if chunk.text:
                    context.streaming_response.queue_text_chunk(chunk.text)
        finally:
            reset_turn_context(context_token)

    except Exception:
        logger.exception("Error during agent execution")
        context.streaming_response.queue_text_chunk(
            "Sorry, I encountered an error while fetching the weather. "
            "Please try again later."
        )
    finally:
        if session is not None:
            state.set_value("ConversationState.agentSession", session.to_dict())
        await context.streaming_response.end_stream()


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    logger.error("Unhandled error: %s", error)
    await context.send_activity("An error occurred. Please try again.")

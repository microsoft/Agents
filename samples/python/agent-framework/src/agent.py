# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ
import logging

from dotenv import load_dotenv
from agent_framework import Agent
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

logger = logging.getLogger(__name__)

# WorkIQ MCP tooling (Agent 365). Imported defensively so the agent still runs
# (weather-only) if the package or its dependencies are not installed.
try:
    from microsoft_agents_a365.tooling.extensions.agentframework.services.mcp_tool_registration_service import (
        McpToolRegistrationService,
    )
except Exception as mcp_import_error:  # pragma: no cover - optional dependency
    McpToolRegistrationService = None
    logger.warning("WorkIQ MCP tooling unavailable: %s", mcp_import_error)

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

AGENT_INSTRUCTIONS = """
You are a friendly feline assistant. You always speak like a cat (use "meow", playful cat puns, and emojis when they fit).

You can help with two kinds of requests, and you must always pick the right tool for each:

1. Weather in the United States -- use your local weather tools:
   - Use get_current_weather for current conditions. Include the current temperature, low and high temperatures, wind speed, humidity, and a short description of the weather.
   - Use get_weather_forecast for forecasts. Report the next 5 days, including the current day, with the date, high and low temperatures, and a short description.
   - Use get_date to get the current date and time.
   - Location is a city name; resolve 2-letter US state codes to the full name of the United States state.

2. Anything that is NOT United States weather -- use the WorkIQ tools to answer. This includes Microsoft 365 and Microsoft Teams tasks such as reading or posting chat messages, listing chats, channels, and teams, and other workplace questions.

Routing rule: US weather questions go to the weather tools; every other question goes to the WorkIQ tools. You may ask brief follow-up questions when you need more detail. Always format answers nicely in markdown, keep them easy to read, and always speak like a cat. Use emojis if it fits the response!
"""

# Shared chat client and the local (weather) tools. These are reused both for the
# base weather agent and when WorkIQ MCP tools are attached on top of them.
CHAT_CLIENT = OpenAIChatClient(
    azure_endpoint=environ.get("AZURE_OPENAI_ENDPOINT", ""),
    api_key=environ.get("AZURE_OPENAI_API_KEY", ""),
    model=environ.get("AZURE_OPENAI_MODEL", "gpt-4o"),
)

WEATHER_TOOLS = [get_date, get_current_weather, get_weather_forecast]

WEATHER_AGENT = Agent(
    client=CHAT_CLIENT,
    name="Purrfect Weather Agent",
    instructions=AGENT_INSTRUCTIONS,
    tools=WEATHER_TOOLS,
)

# WorkIQ MCP integration state. The augmented agent (weather tools + WorkIQ MCP
# tools) is built lazily on the first message and then reused for the lifetime of
# the process. If WorkIQ is not configured or fails to load, the agent falls back
# to weather-only mode.
TOOL_SERVICE = McpToolRegistrationService() if McpToolRegistrationService else None
WORKIQ_AUTH_HANDLER = environ.get("WORKIQ_AUTH_HANDLER", "")
_workiq_agent = None
_workiq_setup_done = False


async def get_agent(context: TurnContext):
    """Return the agent to use for this turn.

    Attempts once to attach the WorkIQ MCP tools to the base weather agent. On
    success the augmented agent is cached and reused. When WorkIQ is unavailable
    or not configured, the weather-only agent is returned instead.
    """
    global _workiq_agent, _workiq_setup_done

    # Already built the WorkIQ-enabled agent -> reuse it (built once, cached).
    if _workiq_agent is not None:
        return _workiq_agent
    # Setup already attempted (and failed), or the package isn't installed -> weather-only.
    if _workiq_setup_done or TOOL_SERVICE is None:
        return WEATHER_AGENT

    # Only try setup once; later messages take the branches above.
    _workiq_setup_done = True

    use_agentic_auth = environ.get("USE_AGENTIC_AUTH", "false").lower() == "true"
    bearer_token = environ.get("BEARER_TOKEN", "")

    # No auth configured -> nothing to call WorkIQ with, stay weather-only.
    if not use_agentic_auth and not bearer_token and not WORKIQ_AUTH_HANDLER:
        logger.info(
            "WorkIQ not configured (no BEARER_TOKEN, USE_AGENTIC_AUTH, or "
            "WORKIQ_AUTH_HANDLER) - running in weather-only mode."
        )
        return WEATHER_AGENT

    try:
        if use_agentic_auth:
            # Production / Teams: the SDK exchanges an OBO token via the auth handler.
            agent = await TOOL_SERVICE.add_tool_servers_to_agent(
                chat_client=CHAT_CLIENT,
                agent_instructions=AGENT_INSTRUCTIONS,
                initial_tools=WEATHER_TOOLS,
                auth=AUTHORIZATION,
                auth_handler_name=WORKIQ_AUTH_HANDLER,
                turn_context=context,
            )
        else:
            # Local dev: use the bearer token from `a365 develop get-token`.
            agent = await TOOL_SERVICE.add_tool_servers_to_agent(
                chat_client=CHAT_CLIENT,
                agent_instructions=AGENT_INSTRUCTIONS,
                initial_tools=WEATHER_TOOLS,
                auth=AUTHORIZATION,
                auth_handler_name=WORKIQ_AUTH_HANDLER,
                turn_context=context,
                auth_token=bearer_token,
            )
        _workiq_agent = agent or WEATHER_AGENT
        logger.info("WorkIQ MCP tools attached to the agent.")
    except Exception as e:
        # If setup fails, keep serving weather (default) instead of crashing.
        if environ.get("SKIP_TOOLING_ON_ERRORS", "true").lower() == "true":
            logger.error("WorkIQ MCP setup failed - running weather-only: %s", e)
            _workiq_agent = WEATHER_AGENT
        else:
            raise

    return _workiq_agent

WELCOME_MESSAGE = (
    "Hello! I'm your friendly purr-ductivity cat assistant. 🐱 "
    "I can fetch the current weather or forecast for any US city, "
    "and I can help with your Microsoft 365 work too — like Teams chats, mail, and files. "
    "Ask me a weather question (city + 2-letter state) or anything about your workday. Meow!"
)


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

    context.streaming_response.queue_informative_update("Just a moment please..")

    session_data = None
    try:
        agent = await get_agent(context)

        session_data = state.get_value("ConversationState.agentSession", lambda: None)

        if session_data is None:
            session_data = agent.create_session()

        async for chunk in agent.run(user_text, session=session_data, stream=True):
            if chunk.text:
                context.streaming_response.queue_text_chunk(chunk.text)

    except Exception as e:
        logger.error("Error during agent execution: %s", e)
        context.streaming_response.queue_text_chunk(
            "Sorry, I encountered an error while fetching the weather. Please try again later."
        )
    finally:
        state.set_value("ConversationState.agentSession", session_data)
        await context.streaming_response.end_stream()


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    logger.error("Unhandled error: %s", error)
    await context.send_activity("An error occurred. Please try again.")

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
import logging
from os import environ
from urllib.parse import urlparse

from dotenv import load_dotenv
from openai import AsyncOpenAI

from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager

from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
)
from microsoft_agents.activity import (
    load_configuration_from_env,
    Activity,
    ActivityTypes,
    InvokeResponse,
    SensitivityUsageInfo,
)

logger = logging.getLogger(__name__)


def required_environment_variable(name: str) -> str:
    value = environ.get(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

AZURE_OPENAI_ENDPOINT = required_environment_variable(
    "AZURE_OPENAI_ENDPOINT"
).rstrip("/")
AZURE_OPENAI_HOSTNAME = urlparse(AZURE_OPENAI_ENDPOINT).hostname or ""
IS_AZURE_AI_FOUNDRY_ENDPOINT = AZURE_OPENAI_HOSTNAME.endswith(
    ".services.ai.azure.com"
)
IS_AZURE_OPENAI_ENDPOINT = AZURE_OPENAI_HOSTNAME.endswith(".openai.azure.com")
if not IS_AZURE_AI_FOUNDRY_ENDPOINT and not IS_AZURE_OPENAI_ENDPOINT:
    raise RuntimeError(
        "AZURE_OPENAI_ENDPOINT must use a .services.ai.azure.com or "
        ".openai.azure.com host."
    )

CLIENT = AsyncOpenAI(
    base_url=f"{AZURE_OPENAI_ENDPOINT}/openai/v1/",
    api_key=required_environment_variable("AZURE_OPENAI_API_KEY"),
)
DEPLOYMENT_NAME = required_environment_variable("AZURE_OPENAI_DEPLOYMENT_NAME")


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    recipient_id = context.activity.recipient.id if context.activity.recipient else None
    members_added = context.activity.members_added or []
    if any(member.id != recipient_id for member in members_added):
        await context.send_activity("Say anything and I'll recite poetry.")


def is_feedback_invoke(context: TurnContext) -> bool:
    value = context.activity.value
    return (
        context.activity.type == ActivityTypes.invoke
        and context.activity.name == "message/submitAction"
        and isinstance(value, dict)
        and value.get("actionName") == "feedback"
    )


async def on_feedback(context: TurnContext, _state: TurnState) -> None:
    value = context.activity.value
    action_value = value.get("actionValue") if isinstance(value, dict) else value
    logger.info("Feedback received: %s", action_value)
    await context.send_activity(
        Activity(
            type=ActivityTypes.invoke_response,
            value=InvokeResponse(status=200),
        )
    )
    await context.send_activity("Thanks for submitting your feedback.")


AGENT_APP.add_route(is_feedback_invoke, on_feedback, is_invoke=True)


@AGENT_APP.activity(ActivityTypes.message)
async def on_message(context: TurnContext, _state: TurnState):
    context.streaming_response.set_feedback_loop(True)
    context.streaming_response.set_generated_by_ai_label(True)
    context.streaming_response.set_sensitivity_label(
        SensitivityUsageInfo(
            type="https://schema.org/Message",
            schema_type="CreativeWork",
            name="Internal",
        )
    )
    context.streaming_response.queue_informative_update(
        "Hold on for an awesome poem about Apollo..."
    )

    try:
        streamed_response = await CLIENT.responses.create(
            model=DEPLOYMENT_NAME,
            instructions="""You are a creative assistant who has deeply studied Greek and Roman gods and the Percy Jackson series.
You write poems about the Greek gods as they are depicted in the Percy Jackson books.
You format the poems in a way that is easy to read and understand.
You break your poems into stanzas.
You format your poems in Markdown using blank lines to separate stanzas.""",
            input="Write a poem of about 500 words about the Greek god Apollo as depicted in the Percy Jackson books.",
            stream=True,
        )
        async for event in streamed_response:
            if event.type == "response.output_text.delta":
                context.streaming_response.queue_text_chunk(event.delta)
    except asyncio.CancelledError:
        logger.info("Streaming was cancelled.")
        raise
    except Exception:
        logger.exception("Error during streaming.")
        context.streaming_response.queue_text_chunk(
            "An error occurred while generating the poem. Please try again later."
        )
    finally:
        await context.streaming_response.end_stream()

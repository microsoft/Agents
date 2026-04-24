# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
import re
import sys
import traceback
from dotenv import load_dotenv

from os import environ
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

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)


AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await context.send_activity(
        "Welcome to the empty agent! "
        "This agent is designed to be a starting point for your own agent development."
    )
    return True

FULL_TEXT = "This is a streaming response."
CHUNKS = FULL_TEXT.split()

@AGENT_APP.message("/stream")
async def on_stream(context: TurnContext, _state: TurnState):
    assert context.streaming_response is not None

    context.streaming_response.queue_informative_update("Starting stream...")
    await asyncio.sleep(1.0)  # Simulate delay before starting stream

    for chunk in CHUNKS[:-1]:
        context.streaming_response.queue_text_chunk(chunk)
        await asyncio.sleep(1.0)  # Simulate delay between chunks

    context.streaming_response.queue_text_chunk(CHUNKS[-1])
    await context.streaming_response.end_stream()


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    await context.send_activity(f"you said: {context.activity.text}")


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    # This check writes out errors to console log .vs. app insights.
    # NOTE: In production environment, you should consider logging this to Azure
    #       application insights.
    print(f"\n [on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()

    # Send a message to the user
    await context.send_activity("The bot encountered an error or bug.")

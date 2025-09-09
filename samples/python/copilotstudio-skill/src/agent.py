# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ

from dotenv import load_dotenv
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
)
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

VERSION = agents_sdk_config.get("version", "unknown")
STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    welcome_text = f"Hello from echo bot, running on version {VERSION}"
    await context.send_activity(welcome_text)


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    text = context.activity.text
    reply_text = f"Echo: {text}"
    await context.send_activity(reply_text)
    if "version" in text.lower():
        await context.send_activity(f"Running on version {VERSION}")

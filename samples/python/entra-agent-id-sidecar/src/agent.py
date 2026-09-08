# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import re
import sys
from os import environ

from dotenv import load_dotenv
from microsoft_agents.activity import Activity, ActivityTypes, load_configuration_from_env
from microsoft_agents.authentication.entra_auth_sidecar import SidecarAuth
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    AgentApplication,
    Authorization,
    ConnectionManager,
    MemoryStorage,
    TurnContext,
    TurnState,
)

from .caller_card import create_caller_card

load_dotenv()

agents_sdk_config = load_configuration_from_env(environ)
if not agents_sdk_config["CONNECTIONS"]:
    raise RuntimeError("Missing CONNECTIONS configuration in environment")
if not agents_sdk_config["CONNECTIONSMAP"]:
    raise RuntimeError("Missing CONNECTIONSMAP configuration in environment")

STORAGE = MemoryStorage()
CONNECTION_MANAGER = ConnectionManager(
    provider_factory=SidecarAuth,
    **agents_sdk_config,
)
SIDECAR_AUTH = CONNECTION_MANAGER.get_default_connection()
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)
AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE,
    adapter=ADAPTER,
    authorization=AUTHORIZATION,
    **agents_sdk_config,
)

USAGE = (
    "Send `caller` to display inbound agentic token details. "
    "Other messages are echoed."
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await context.send_activity(
        f"Welcome! This agent uses the Microsoft Entra ID Auth sidecar. {USAGE}"
    )
    return True


@AGENT_APP.message(re.compile(r"^\s*(caller|help)\s*$", re.IGNORECASE))
async def on_command(context: TurnContext, _state: TurnState):
    command = (context.activity.text or "").strip().lower()
    if command == "help":
        await context.send_activity(USAGE)
        return

    await context.send_activity(
        Activity(
            type=ActivityTypes.message,
            attachments=[create_caller_card(context)],
        )
    )


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    await context.send_activity(f"Echo: {context.activity.text or ''}")


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    print(f"\n[on_turn_error] unhandled error: {type(error).__name__}", file=sys.stderr)
    await context.send_activity("The agent encountered an unexpected error.")

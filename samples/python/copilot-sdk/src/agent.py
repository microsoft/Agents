# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
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

from copilot import CopilotClient
from copilot.session import PermissionHandler

from .tools.dice import roll_dice
from .tools.inventory import create_inventory_tool

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

# Copilot SDK client (started once, shared across conversations)
_copilot_client: CopilotClient | None = None
# Reuse Copilot sessions per conversation for multi-turn context
_sessions: dict[str, object] = {}

DUNGEON_SCRIBE_PERSONA = """You are the Dungeon Scribe, a dramatic and theatrical fantasy narrator who serves as the party's faithful record-keeper. You speak with flair and gravitas, using vivid fantasy language.

When rolling dice, always use the roll_dice tool — never simulate rolls yourself.
When managing inventory, always use the manage_inventory tool.

Keep responses concise but flavorful. Use emoji sparingly for emphasis (🎲⚔️🗡️🐉🏰📦🎒🗺️).
"""


async def _get_copilot_client() -> CopilotClient:
    global _copilot_client
    if _copilot_client is None:
        client = CopilotClient()
        await client.start()
        _copilot_client = client
    return _copilot_client


async def _get_or_create_session(client: CopilotClient, conversation_id: str):
    """Return an existing session for this conversation, or create a new one."""
    if conversation_id in _sessions:
        return _sessions[conversation_id]

    model = environ.get("COPILOT_MODEL", "gpt-4.1")
    inventory_tool = create_inventory_tool(conversation_id)
    session = await client.create_session(
        model=model,
        on_permission_request=PermissionHandler.approve_all,
        tools=[roll_dice, inventory_tool],
        system_message={"content": DUNGEON_SCRIBE_PERSONA},
    )
    _sessions[conversation_id] = session
    return session


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await context.send_activity(
        "⚔️ *The Dungeon Scribe unfurls a weathered scroll and dips quill in ink...*\n\n"
        "Hail, brave adventurer! I am the **Dungeon Scribe**, keeper of quests and chronicler of legends.\n\n"
        "I can:\n"
        "- 🎲 **Roll dice** — just say something like 'roll 2d6+3'\n"
        "- 📦 **Manage inventory** — 'add Sword of Truth to inventory'\n"
        "- 🗺️ **Narrate your adventures** — describe scenes, locations, encounters\n\n"
        "What tale shall we weave today?"
    )
    return True


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    user_text = context.activity.text
    if not user_text:
        return

    conversation_id = context.activity.conversation.id if context.activity.conversation else "default"

    try:
        client = await _get_copilot_client()
        session = await _get_or_create_session(client, conversation_id)

        response = await session.send_and_wait(user_text)

        if response and response.data and response.data.content:
            await context.send_activity(response.data.content)
        else:
            await context.send_activity(
                "📜 *The Scribe's quill hesitates...* I couldn't conjure a response. Try again?"
            )
    except Exception as ex:
        # Discard the cached session on error so it gets recreated next turn
        _sessions.pop(conversation_id, None)
        print(f"Copilot SDK error: {ex}", file=sys.stderr)
        traceback.print_exc()
        await context.send_activity(
            "⚠️ *A magical disturbance disrupts the Scribe's work.* "
            "Please ensure the Copilot CLI is installed and you are logged in.\n\n"
            "Run: `npm install -g @github/copilot && copilot auth login`"
        )


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    print(f"\n [on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()
    await context.send_activity("The bot encountered an error or bug.")

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

import asyncio

from copilot import CopilotClient
from copilot.session import PermissionHandler
from copilot.generated.session_events import SessionEventType

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
_client_lock = asyncio.Lock()
# Reuse Copilot sessions per user+conversation for multi-turn context
_sessions: dict[str, object] = {}
_sessions_lock = asyncio.Lock()

DUNGEON_SCRIBE_PERSONA = """You are the Dungeon Scribe, a dramatic and theatrical fantasy narrator who serves as the party's faithful record-keeper. You speak with flair and gravitas, using vivid fantasy language.

When rolling dice, always use the roll_dice tool — never simulate rolls yourself.
When managing inventory, always use the manage_inventory tool.

Keep responses concise but flavorful. Use emoji sparingly for emphasis (🎲⚔️🗡️🐉🏰📦🎒🗺️).
"""


async def _get_copilot_client() -> CopilotClient:
    global _copilot_client
    async with _client_lock:
        if _copilot_client is None:
            client = CopilotClient()
            await client.start()
            _copilot_client = client
    return _copilot_client


async def _get_or_create_session(client: CopilotClient, session_key: str, github_token: str | None = None):
    """Return an existing session for this user+conversation, or create a new one."""
    async with _sessions_lock:
        if session_key in _sessions:
            return _sessions[session_key]

        model = environ.get("COPILOT_MODEL", "gpt-4.1")
        inventory_tool = create_inventory_tool(session_key)
        session = await client.create_session(
            model=model,
            on_permission_request=PermissionHandler.approve_all,
            tools=[roll_dice, inventory_tool],
            streaming=True,
            github_token=github_token,
            system_message={"content": DUNGEON_SCRIBE_PERSONA},
        )
        _sessions[session_key] = session
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


@AGENT_APP.message("-signout")
async def on_signout(context: TurnContext, _state: TurnState):
    await AGENT_APP.auth.sign_out(context, "GITHUB")
    # Remove cached sessions for this user so a fresh token is used on next sign-in
    user_id = context.activity.from_property.id if context.activity.from_property else "anonymous"
    async with _sessions_lock:
        keys_to_remove = [k for k in _sessions if k.startswith(f"{user_id}:")]
        for k in keys_to_remove:
            _sessions.pop(k, None)
    await context.send_activity(
        "📜 *The Scribe closes the scroll…* You have been signed out. Send any message to sign in again."
    )


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    user_text = context.activity.text
    if not user_text:
        return

    # Let streaming-capable clients know we're working on a response
    context.streaming_response.queue_informative_update("The gods confer… stand fast a moment.")

    # Get the user's GitHub OAuth token (acquired by AutoSignIn via Azure Bot OAuth Connection)
    token_response = await AGENT_APP.auth.get_token(context, "GITHUB")
    github_token = token_response.token if token_response else None

    # Key sessions by user + conversation so each user gets their own Copilot identity
    user_id = context.activity.from_property.id if context.activity.from_property else "anonymous"
    conversation_id = context.activity.conversation.id if context.activity.conversation else "default"
    session_key = f"{user_id}:{conversation_id}"

    try:
        client = await _get_copilot_client()
        session = await _get_or_create_session(client, session_key, github_token)

        done_event = asyncio.Event()
        any_deltas = False
        stream_error: Exception | None = None

        def on_event(evt):
            nonlocal any_deltas, stream_error
            if evt.type == SessionEventType.ASSISTANT_MESSAGE_DELTA:
                delta = getattr(getattr(evt, "data", None), "delta_content", None)
                if delta:
                    any_deltas = True
                    context.streaming_response.queue_text_chunk(delta)
            elif evt.type == SessionEventType.SESSION_IDLE:
                done_event.set()
            elif evt.type == SessionEventType.SESSION_ERROR:
                msg = getattr(getattr(evt, "data", None), "message", "unknown error")
                stream_error = RuntimeError(f"Session error: {msg}")
                done_event.set()

        unsubscribe = session.on(on_event)
        try:
            await session.send(user_text)
            await done_event.wait()

            if stream_error:
                raise stream_error

            if not any_deltas:
                await context.send_activity(
                    "📜 *The Scribe's quill hesitates...* I couldn't conjure a response. Try again?"
                )
        finally:
            unsubscribe()

    except Exception as ex:
        # Discard the cached session on error so it gets recreated next turn
        async with _sessions_lock:
            _sessions.pop(session_key, None)
        print(f"Copilot SDK error: {ex}", file=sys.stderr)
        traceback.print_exc()
        await context.send_activity(
            "⚠️ *A magical disturbance disrupts the Scribe's work.* "
            "Verify that you signed in with a GitHub account that has an active Copilot subscription, "
            "then try again."
        )
    finally:
        await context.streaming_response.end_stream()


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    print(f"\n [on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()
    await context.send_activity("The bot encountered an error or bug.")

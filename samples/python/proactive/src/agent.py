# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
import logging
import re
from os import environ

from dotenv import load_dotenv
from microsoft_agents.activity import Activity, Attachment, load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    AgentApplication,
    Authorization,
    MemoryStorage,
    TurnContext,
    TurnState,
)
from microsoft_agents.hosting.core.app import ApplicationOptions, ProactiveOptions
from microsoft_agents.hosting.core.app.proactive import Conversation

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

# Environment variables represent indexed values such as SCOPES__0 as a
# dictionary. The connection configuration requires an ordered list.
for connection in agents_sdk_config.get("CONNECTIONS", {}).values():
    settings = connection.get("SETTINGS", {})
    scopes = settings.get("SCOPES")
    if isinstance(scopes, dict):
        settings["SCOPES"] = [scopes[key] for key in sorted(scopes, key=int)]

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    options=ApplicationOptions(
        storage=STORAGE,
        adapter=ADAPTER,
        proactive=ProactiveOptions(storage=STORAGE),
    ),
    authorization=AUTHORIZATION,
)


WELCOME_CARD = {
    "type": "AdaptiveCard",
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "version": "1.5",
    "body": [
        {"type": "TextBlock", "text": "Welcome to the Proactive sample.", "weight": "Bolder", "size": "Medium", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "Commands:", "weight": "Bolder", "spacing": "Medium", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "• -s: Store this conversation.", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "• -c: Continue this conversation proactively.", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "• -c <conversation-id>: Continue a stored conversation.", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "• -convo: Show the conversation data for the HTTP example.", "wrap": True, "horizontalAlignment": "Left"},
        {"type": "TextBlock", "text": "Send other text to echo it from a proactive turn.", "spacing": "Medium", "wrap": True, "horizontalAlignment": "Left"},
    ],
}


async def on_continue_conversation(
    context: TurnContext, _state: TurnState
) -> None:
    await context.send_activity("This is OnContinueConversation")


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState) -> None:
    agent_id = context.activity.recipient.id if context.activity.recipient else None
    members_added = context.activity.members_added or []
    if any(member.id != agent_id for member in members_added):
        await context.send_activity(
            Activity(
                type="message",
                attachments=[
                    Attachment(
                        content_type="application/vnd.microsoft.card.adaptive",
                        content=WELCOME_CARD,
                    )
                ],
            )
        )


@AGENT_APP.message("-s")
async def on_store(context: TurnContext, _state: TurnState) -> None:
    conversation = Conversation.from_turn_context(context)
    await AGENT_APP.proactive.store_conversation(conversation)
    conversation_id = conversation.conversation_reference.conversation.id
    await context.send_activity(
        "Your conversation has been stored. Send a POST request to "
        f"/proactive/sendActivity/{conversation_id} to trigger a proactive message."
    )


@AGENT_APP.message("-convo")
async def on_conversation_data(context: TurnContext, _state: TurnState) -> None:
    conversation = Conversation.from_turn_context(context)
    payload = {
        "reference": conversation.conversation_reference.model_dump(
            mode="json", by_alias=True, exclude_unset=True
        ),
        "claims": conversation.claims,
    }
    await context.send_activity(json.dumps(payload))


@AGENT_APP.message(re.compile(r"^-c(?:\s+\S+)?\s*$"))
async def on_continue(context: TurnContext, _state: TurnState) -> None:
    parts = (context.activity.text or "").strip().split()

    if len(parts) == 1:
        conversation = Conversation.from_turn_context(context)
    else:
        conversation_id = parts[1]
        conversation = await AGENT_APP.proactive.get_conversation(conversation_id)
        if conversation is None:
            await context.send_activity(
                f"Conversation '{conversation_id}' was not found. "
                "Send -s first to store it."
            )
            return

    await AGENT_APP.proactive.continue_conversation(
        ADAPTER, conversation, on_continue_conversation
    )


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState) -> None:
    conversation = Conversation.from_turn_context(context)
    continuation = conversation.conversation_reference.get_continuation_activity()
    continuation.value = context.activity

    async def on_echo(continued_context: TurnContext, _continued_state: TurnState) -> None:
        original = continued_context.activity.value
        if isinstance(original, Activity):
            text = original.text
        elif isinstance(original, dict):
            text = original.get("text")
        else:
            text = None
        await continued_context.send_activity(f"You said: {text or ''}")

    await AGENT_APP.proactive.continue_conversation(
        ADAPTER,
        conversation,
        on_echo,
        continuation_activity=continuation,
    )


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception) -> None:
    logging.error("Unhandled error in Proactive sample: %s", error)
    await context.send_activity("The agent encountered an unexpected error.")

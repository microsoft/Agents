# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import json
from os import environ
from dotenv import load_dotenv

from microsoft_agents.hosting.core import (
    Authorization,
    TurnContext,
    MemoryStorage,
    AgentApplication,
    TurnState,
    MemoryStorage,
    MessageFactory,
)
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager

from .card_messages import CardMessages

# Load configuration from environment
load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

adaptive_card_json = json.load(open("src/resources/adaptive_card.json", "r"))

# Create storage and connection manager
STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await CardMessages.send_intro_card(context)


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    if (
        context.activity.text is not None
        and context.activity.recipient.id != context.activity.from_property.id
    ):
        pre = context.activity.text.lower()[0].lower()

        funcs = {
            "display card options": CardMessages.send_intro_card,
            "2": CardMessages.send_animation_card,
            "3": CardMessages.send_audio_card,
            "4": CardMessages.send_hero_card,
            "5": CardMessages.send_receipt_card,
            "6": CardMessages.send_thumbnail_card,
            "7": CardMessages.send_video_card,
        }

        if pre in funcs:
            await funcs[pre](context)
        elif pre == "1":
            await CardMessages.send_adaptive_card(context, adaptive_card_json)
        else:
            await context.send_activity(
                MessageFactory.text("Your input was not recognized, please try again.")
            )
            await CardMessages.send_intro_card(context)
    else:
        await context.send_activity(
            "This sample is only for testing Cards using CardFactory methods."
            "Please refer to other samples to test out more functionalities"
        )


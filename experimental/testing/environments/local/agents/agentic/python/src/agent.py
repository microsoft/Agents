# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging
import os
import jwt
from dotenv import load_dotenv

from os import environ
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
    MessageFactory,
)
from microsoft_agents.hosting.core.storage import (
    TranscriptLoggerMiddleware,
    ConsoleTranscriptLogger,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.activity import load_configuration_from_env, Attachment

from .jwtcard import load_adaptive_card, update_card_data

logger = logging.getLogger(__name__)

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
ADAPTER.use(TranscriptLoggerMiddleware(ConsoleTranscriptLogger()))
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

@AGENT_APP.message("/agentic", auth_handlers=["AGENTIC"])
async def on_message(context: TurnContext, _state: TurnState):

    breakpoint()
    aau_token = await AGENT_APP.auth.get_token(context, "AGENTIC")
    decoded = jwt.decode(aau_token.token, options={"verify_signature": False})
    decoded["length"] = len(aau_token.token)

    # relative_path = os.path.abspath(os.path.dirname(__file__))
    # template_path = os.path.join(relative_path, "JWTDecodeCard.json")

    # card = load_adaptive_card(template_path)
    # populated_card = update_card_data(card, decoded)

    # attachment = MessageFactory.attachment(
    #     Attachment(
    #         content_type="application/vnd.microsoft.card.adaptive",
    #         content=populated_card,
    #     )
    # )
    # await context.send_activity(attachment)

    await context.send_activity(
        f"Acquired agentic user token with length: {len(aau_token.token)}"
    )

@AGENT_APP.activity("message")
async def on_message_activity(context: TurnContext, _state: TurnState):
    await context.send_activity(f"You said: {context.activity.text}")

@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    logger.error("[on_turn_error] unhandled error: %s", error)

    # Send a message to the user
    await context.send_activity("The agent encountered an error.")

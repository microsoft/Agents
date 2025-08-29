# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import re
from os import environ
from dotenv import load_dotenv

from microsoft_agents.hosting.core import (
    Authorization,
    TurnContext,
    MemoryStorage,
    AgentApplication,
    TurnState,
    MemoryStorage,
)
from microsoft_agents.activity import load_configuration_from_env, ActivityTypes
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.copilotstudio.client import ConnectionSettings, CopilotClient, PowerPlatformEnvironment, PowerPlatformCloud

# Load configuration from environment
load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

# Create storage and connection manager
STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

# just for testing
# in practice, use a more robust method to manage app state with conversation state
mcs_convo_id = None

async def get_client(context: TurnContext) -> CopilotClient:
    
    settings = ConnectionSettings(
        environment_id=environ.get("COPILOTSTUDIOAGENT__ENVIRONMENTID"),
        agent_identifier=environ.get("COPILOTSTUDIOAGENT__SCHEMANAME"),
        cloud=PowerPlatformCloud.PROD,
        copilot_agent_type=None,
        custom_power_platform_cloud=None,
    )

    scope = PowerPlatformEnvironment.get_token_audience(settings)

    # where are exchanging the token every time. You can be smarter about this.
    token_response = await AGENT_APP.auth.exchange_token(context, [scope], "MCS")
    mcs_client = CopilotClient(settings, token_response.token)

    return mcs_client

@AGENT_APP.message("/signout")
async def signout(context: TurnContext, state: TurnState):
    # Force a user signout to reset the user state
    # This is needed to reset the token in Azure Bot Services if needed. 
    # Typically this wouldn't be need in a production Agent.  Made available to assist it starting from scratch.
    await AGENT_APP.auth.sign_out(context, state)
    await context.send_activity("You have signed out")

# Since Auto SignIn is enabled, by the time this is called the token is already available via Authorization.get_token or
# Authorization.exchange_token.
# NOTE: This is a slightly unusual way to handle incoming Activities (but perfectly) valid.  For this sample,
# we just want to proxy messages to/from a Copilot Studio Agent.
@AGENT_APP.message(re.compile(r".*"), auth_handlers=["MCS"])
async def default_handler(context: TurnContext, _state: TurnState):
    global mcs_convo_id

    mcs_client = await get_client(context)

    if not mcs_convo_id:
        async for reply in mcs_client.start_conversation():
            if reply.type == ActivityTypes.message:
                await context.send_activity(reply.text)
                mcs_convo_id = reply.conversation.id
    elif context.activity.type == ActivityTypes.message:
        async for reply in mcs_client.ask_question(context.activity.text, mcs_convo_id):
            if reply.type == ActivityTypes.message:
                await context.send_activity(reply.text)
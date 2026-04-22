# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import re
import sys
import traceback
from os import environ

from azure.identity.aio import DefaultAzureCredential
from dotenv import load_dotenv
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
)
from microsoft_agents.storage.blob import BlobStorage, BlobStorageConfig

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

_credential = DefaultAzureCredential()
_blob_config = BlobStorageConfig(
    container_name=environ["BLOB_CONTAINER_NAME"],
    url=environ["BLOB_STORAGE_ACCOUNT_URL"],
    credential=_credential,
)

STORAGE = BlobStorage(_blob_config)
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


@AGENT_APP.message(re.compile(r"^hello$"))
async def on_hello(context: TurnContext, _state: TurnState):
    await context.send_activity("Hello!")


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, _state: TurnState):
    await context.send_activity(f"you said: {context.activity.text}")


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    print(f"\n [on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()
    await context.send_activity("The bot encountered an error or bug.")

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
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.storage.blob import BlobStorage, BlobStorageConfig

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

# Read Azure Blob Storage configuration from environment variables.
# Set AZURE_BLOB_STORAGE_CONNECTION_STRING in your .env file or environment.
# For local development with Azurite, use: UseDevelopmentStorage=true
_connection_string = os.environ.get("AZURE_BLOB_STORAGE_CONNECTION_STRING")
if not _connection_string:
    raise RuntimeError(
        "AZURE_BLOB_STORAGE_CONNECTION_STRING is required. "
        "Set it in your .env file or environment variables. "
        "For local development with Azurite, use: UseDevelopmentStorage=true"
    )

_container_name = os.environ.get(
    "AZURE_BLOB_STORAGE_CONTAINER_NAME", "agents-persistent-state"
)

# Register storage backed by Azure Blob Storage so that conversation state
# persists across agent restarts and operates correctly in a cluster.
# The container is created automatically on first use.
STORAGE = BlobStorage(BlobStorageConfig(
    connection_string=_connection_string,
    container_name=_container_name,
))

CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await context.send_activity("Hello and Welcome!")
    return True


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, state: TurnState):
    # Increment count — persisted to Blob Storage on each turn
    count = (state.conversation.get("count") or 0) + 1
    state.conversation["count"] = count

    await context.send_activity(f"[{count}] you said: {context.activity.text}")


@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    print(f"\n [on_turn_error] unhandled error: {error}", file=sys.stderr)
    traceback.print_exc()
    await context.send_activity("The bot encountered an error or bug.")

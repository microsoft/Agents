# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# enable logging for Microsoft Agents library
# for more information, see README.md for Quickstart Agent
import logging
ms_agents_logger = logging.getLogger("microsoft_agents")
ms_agents_logger.addHandler(logging.StreamHandler())
ms_agents_logger.setLevel(logging.INFO)

import sys
from os import environ
import asyncio
import webbrowser

from dotenv import load_dotenv

from msal import PublicClientApplication

from microsoft_agents.activity import ActivityTypes, load_configuration_from_env
from microsoft_agents.copilotstudio.client import (
    ConnectionSettings,
    CopilotClient,
)

from .local_token_cache import LocalTokenCache

logger = logging.getLogger(__name__)

load_dotenv()

TOKEN_CACHE = LocalTokenCache("./.local_token_cache.json")


async def open_browser(url: str):
    logger.debug(f"Opening browser at {url}")
    await asyncio.get_event_loop().run_in_executor(None, lambda: webbrowser.open(url))


def acquire_token(settings: ConnectionSettings, app_client_id, tenant_id):
    pca = PublicClientApplication(
        client_id=app_client_id,
        authority=f"https://login.microsoftonline.com/{tenant_id}",
        token_cache=TOKEN_CACHE,
    )

    token_request = {
        "scopes": ["https://api.powerplatform.com/.default"],
    }
    accounts = pca.get_accounts()
    retry_interactive = False
    token = None
    try:
        if accounts:
            response = pca.acquire_token_silent(
                token_request["scopes"], account=accounts[0]
            )
            token = response.get("access_token")
        else:
            retry_interactive = True
    except Exception as e:
        retry_interactive = True
        logger.error(
            f"Error acquiring token silently: {e}. Going to attempt interactive login."
        )

    if retry_interactive:
        logger.debug("Attempting interactive login...")
        response = pca.acquire_token_interactive(**token_request)
        token = response.get("access_token")

    return token


def create_client():
    settings = ConnectionSettings(
        environment_id=environ.get("COPILOTSTUDIOAGENT__ENVIRONMENTID"),
        agent_identifier=environ.get("COPILOTSTUDIOAGENT__SCHEMANAME"),
        cloud=None,
        copilot_agent_type=None,
        custom_power_platform_cloud=None,
    )
    token = acquire_token(
        settings,
        app_client_id=environ.get("COPILOTSTUDIOAGENT__AGENTAPPID"),
        tenant_id=environ.get("COPILOTSTUDIOAGENT__TENANTID"),
    )
    copilot_client = CopilotClient(settings, token)
    return copilot_client


async def ainput(string: str) -> str:
    await asyncio.get_event_loop().run_in_executor(
        None, lambda s=string: sys.stdout.write(s + " ")
    )
    return await asyncio.get_event_loop().run_in_executor(None, sys.stdin.readline)


async def ask_question(copilot_client, conversation_id):
    query = (await ainput("\n>>>: ")).lower().strip()
    if query in ["exit", "quit"]:
        print("Exiting...")
        return
    if query:
        replies = copilot_client.ask_question(query, conversation_id)
        async for reply in replies:
            if reply.type == ActivityTypes.message:
                print(f"\n{reply.text}")
                if reply.suggested_actions:
                    for action in reply.suggested_actions.actions:
                        print(f" - {action.title}")
            elif reply.type == ActivityTypes.end_of_conversation:
                print("\nEnd of conversation.")
                sys.exit(0)
        await ask_question(copilot_client, conversation_id)


async def main():
    copilot_client = create_client()
    act = copilot_client.start_conversation(True)
    print("\nSuggested Actions: ")
    async for action in act:
        if action.text:
            print(action.text)
    await ask_question(copilot_client, action.conversation.id)


asyncio.run(main())

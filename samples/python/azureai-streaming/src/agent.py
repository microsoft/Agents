# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from os import environ
import logging

from dotenv import load_dotenv
from openai import AsyncAzureOpenAI

from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager

from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
)
from microsoft_agents.activity import (
    load_configuration_from_env,
    Activity,
    ActivityTypes,
    SensitivityUsageInfo
)

logger = logging.getLogger(__name__)

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)

from azure.identity import DefaultAzureCredential, get_bearer_token_provider
token_provider = get_bearer_token_provider(DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default")
CLIENT = AsyncAzureOpenAI(
    api_version=environ["AZURE_OPENAI_API_VERSION"],
    azure_endpoint=environ["AZURE_OPENAI_ENDPOINT"],
    azure_ad_token_provider=token_provider
)

@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    await context.send_activity("Welcome to the streaming sample. Type **poem** to see the streaming feature in action!")

@AGENT_APP.activity("invoke")
async def invoke(context: TurnContext, _state: TurnState) -> str:
    """
    Internal method to process template expansion or function invocation.
    """
    invoke_response = Activity(
        type=ActivityTypes.invoke_response, value={"status": 200}
    )
    print(f"Invoke activity received: {context.activity}")
    await context.send_activity(invoke_response)

@AGENT_APP.message("poem")
async def on_poem_message(context: TurnContext, _state: TurnState):
    context.streaming_response.set_feedback_loop(True)
    context.streaming_response.set_generated_by_ai_label(True)
    context.streaming_response.set_sensitivity_label(
        SensitivityUsageInfo(
            type="https://schema.org/Message",
            schema_type="CreativeWork",
            name="Internal",
        )
    )
    context.streaming_response.queue_informative_update("Starting a poem...\n")

    streamed_response = await CLIENT.chat.completions.create(
        model="gpt-4o",
        messages=[
            {"role": "system", "content": """You are a creative assistant who has deeply studied Greek and Roman Gods, You also know all of the Percy Jackson Series
You write poems about the Greek Gods as they are depicted in the Percy Jackson books.
You format the poems in a way that is easy to read and understand
You break your poems into stanzas 
You format your poems in Markdown using double lines to separate stanzas
Invent 2 citations"""},
            {"role": "user", "content": "Write a poem in no less than 500 words about the Greek God Apollo as depicted in the Percy Jackson books"}
        ],
        stream=True,
    )
    try:
        async for chunk in streamed_response:
            if chunk.choices and chunk.choices[0].delta.content:
                context.streaming_response.queue_text_chunk(chunk.choices[0].delta.content)
    except Exception as e:
        logger.error(f"Error during streaming: {e}")
        context.streaming_response.queue_text_chunk("An error occurred while generating the poem. Please try again later.")
    finally:
        await context.streaming_response.end_stream()

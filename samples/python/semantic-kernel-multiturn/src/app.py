from os import environ

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from semantic_kernel.contents import ChatHistory

from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MessageFactory,
    MemoryStorage,
)
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.authentication.msal import MsalConnectionManager

from microsoft_agents.activity import Attachment, load_configuration_from_env

from .agent import WeatherForecastAgent

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)

token_provider = get_bearer_token_provider(
    DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"
)

AGENT = WeatherForecastAgent(
    AzureChatCompletion(
        api_version=environ["AZURE_OPENAI_API_VERSION"],
        endpoint=environ["AZURE_OPENAI_ENDPOINT"],
        ad_token_provider=token_provider,
        deployment_name=environ.get("AZURE_OPENAI_DEPLOYMENT_NAME", "gpt-4o"),
    )
)

AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    members_added = context.activity.members_added
    for member in members_added:
        if member.id != context.activity.recipient.id:
            await context.send_activity("Hello and welcome!")


@AGENT_APP.activity("message")
async def on_message(context: TurnContext, state: TurnState):

    context.streaming_response.queue_informative_update(
        "Working on a response for you..."
    )

    chat_history = state.get_value(
        "ConversationState.chatHistory", lambda: ChatHistory(), target_cls=ChatHistory
    )

    forecast_response = await AGENT.invoke_agent(context.activity.text, chat_history)
    if forecast_response is None:
        context.streaming_response.queue_text_chunk(
            "Sorry, I couldn't get the weather forecast at the moment."
        )
    elif forecast_response.contentType == "AdaptiveCard":
        context.streaming_response.set_attachments(
            [
                Attachment(
                    content_type="application/vnd.microsoft.card.adaptive",
                    content=forecast_response.content,
                )
            ]
        )
    else:
        context.streaming_response.queue_text_chunk(forecast_response.content)

    await context.streaming_response.end_stream()

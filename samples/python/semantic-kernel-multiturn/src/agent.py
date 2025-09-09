import json
from typing import Union, Literal, Any

from pydantic import BaseModel

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import OpenAIPromptExecutionSettings
from semantic_kernel.connectors.ai.function_choice_behavior import (
    FunctionChoiceBehavior,
)
from semantic_kernel.functions import KernelArguments
from semantic_kernel.contents import ChatHistory
from semantic_kernel.connectors.ai.open_ai import AzureChatCompletion
from semantic_kernel.agents import ChatCompletionAgent, ChatHistoryAgentThread

from src.plugins import DateTimePlugin, WeatherForecastPlugin, AdaptiveCardPlugin


class WeatherForecastAgentResponse(BaseModel):
    contentType: str = Literal["Text", "AdaptiveCard"]
    content: Union[dict, str]


class WeatherForecastAgent:

    agent_name = "WeatherForecastAgent"

    agent_instructions = """
            You are a friendly assistant that helps people find a weather forecast for a given time and place.
            You may ask follow up questions until you have enough information to answer the customers question,
            but once you have a forecast forecast, make sure to format it nicely using an adaptive card.
            You should use adaptive JSON format to display the information in a visually appealing way
            You should include a button for more details that points at https://www.msn.com/en-us/weather/forecast/in-{location} (replace {location} with the location the user asked about).
            You should use adaptive cards version 1.5 or later.
            
            Respond only in JSON format with the following JSON schema:
            
            {
                "contentType": "'Text' or 'AdaptiveCard' only",
                "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
            }
            """

    def __init__(self, client: AzureChatCompletion):

        self.client = client

        execution_settings = OpenAIPromptExecutionSettings()
        execution_settings.function_choice_behavior = FunctionChoiceBehavior.Auto()
        execution_settings.temperature = 0
        execution_settings.top_p = 1
        self.execution_settings = execution_settings

    async def invoke_agent(
        self, input: str, chat_history: ChatHistory
    ) -> dict[str, Any]:

        thread = ChatHistoryAgentThread()
        kernel = Kernel()

        chat_history.add_user_message(input)

        agent = ChatCompletionAgent(
            service=self.client,
            name=WeatherForecastAgent.agent_name,
            instructions=WeatherForecastAgent.agent_instructions,
            kernel=kernel,
            arguments=KernelArguments(
                chat_history=ChatHistory(),
                settings=self.execution_settings,
                kernel=kernel,
            ),
        )

        agent.kernel.add_plugin(plugin=DateTimePlugin(), plugin_name="datetime")
        kernel.add_plugin(plugin=AdaptiveCardPlugin(), plugin_name="adaptiveCard")
        kernel.add_plugin(plugin=WeatherForecastPlugin(), plugin_name="weatherForecast")

        resp: str = ""

        async for chat in agent.invoke(chat_history, thread=thread):
            chat_history.add_message(chat.content)
            resp += chat.content.content

        # if resp has a json\n prefix, remove it
        if "json\n" in resp:
            resp = resp.replace("json\n", "")
            resp = resp.replace("```", "")

        resp = resp.strip()

        try:
            json_node: dict = json.loads(resp)
            result = WeatherForecastAgentResponse.model_validate(json_node)
            return result
        except Exception as e:
            return await self.invoke_agent(
                "That response did not match the expected format. Please try again. Error: "
                + str(e),
                chat_history,
            )

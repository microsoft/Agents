from semantic_kernel.functions import kernel_function
from semantic_kernel.connectors.ai.open_ai import OpenAIPromptExecutionSettings
from semantic_kernel.contents import ChatHistory


class AdaptiveCardPlugin:

    @kernel_function()
    async def get_adaptive_card_for_data(self, data: str, kernel) -> str:

        instructions = """
        When given data about the weather forecast for a given time and place, generate an adaptive card
        that displays the information in a visually appealing way. Only return the valid adaptive card
        JSON string in the response.
        """

        # Set up chat
        chat = ChatHistory(instructions=instructions)
        chat.add_user_message(data)

        chat_completion = kernel.get_service("adaptive_card_service")

        # Get the response
        result = await chat_completion.get_chat_message_contents(
            chat, OpenAIPromptExecutionSettings()
        )

        # Extract the message text (if result is a list of ChatMessageContent)
        message = result[0].content if result else "No response"

        return message

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Agents SDK application and Adaptive Card behavior."""

from collections.abc import Mapping

from microsoft_agents.activity import (
    Activity,
    ActivityTypes,
)
from microsoft_teams.api import AdaptiveCardActionMessageResponse
from microsoft_agents.hosting.core import AgentApplication, MemoryStorage, TurnContext
from microsoft_agents.hosting.core.authorization import Connections
from microsoft_agents.hosting.msteams import TeamsAgentExtension

CARD_ACTIONS_COMMAND = "card actions"
TOGGLE_VISIBILITY_COMMAND = "toggle visibility"
SUBMIT_NAME_VERB = "submit_name"
HELP_TEXT = (
    "Welcome to the Agent Cards sample. Mention me with 'card actions' or "
    "'toggle visibility' to try the cards."
)


def create_card_actions_card() -> dict:
    """Build the nested action card and required name form."""
    inner_card = {
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [
            {"type": "TextBlock", "text": "**Welcome To Your New Card**"},
            {
                "type": "TextBlock",
                "text": "This is your new card inside another card",
            },
        ],
    }
    middle_card = {
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [
            {
                "type": "TextBlock",
                "text": "This card's action will show another card",
            }
        ],
        "actions": [
            {
                "type": "Action.ShowCard",
                "title": "Action.ShowCard",
                "card": inner_card,
            }
        ],
    }
    name_form_card = {
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [
            {
                "type": "Input.Text",
                "id": "name",
                "label": "Please enter your name:",
                "isRequired": True,
                "errorMessage": "Name is required",
            }
        ],
        "actions": [
            {
                "type": "Action.Execute",
                "title": "Submit",
                "verb": SUBMIT_NAME_VERB,
                "data": {},
                "associatedInputs": "auto",
            }
        ],
    }
    return {
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [{"type": "TextBlock", "text": "Adaptive Card Actions"}],
        "actions": [
            {
                "type": "Action.OpenUrl",
                "title": "Action Open URL",
                "url": "https://adaptivecards.io",
            },
            {
                "type": "Action.ShowCard",
                "title": "Action Submit",
                "card": name_form_card,
            },
            {
                "type": "Action.ShowCard",
                "title": "Action ShowCard",
                "card": middle_card,
            },
        ],
    }


def create_toggle_visibility_card() -> dict:
    """Build a card with its greeting initially hidden."""
    return {
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [
            {
                "type": "TextBlock",
                "text": "Click to show or hide the message",
            },
            {
                "type": "TextBlock",
                "id": "helloWorld",
                "isVisible": False,
                "text": "**Hello World!**",
                "size": "ExtraLarge",
            },
        ],
        "actions": [
            {
                "type": "Action.ToggleVisibility",
                "title": "Click me!",
                "targetElements": ["helloWorld"],
            }
        ],
    }


def card_attachment(card: dict):
    """Wrap an Adaptive Card in the activity attachment expected by Teams."""
    from microsoft_agents.hosting.core import CardFactory

    return CardFactory.adaptive_card(card)


async def handle_name_submission(
    context: TurnContext, _state, action_data: Mapping[str, object] | None
) -> AdaptiveCardActionMessageResponse:
    submitted_name = action_data.get("name", "") if action_data else ""
    await context.send_activity(f"Data Submitted: {submitted_name}")
    return AdaptiveCardActionMessageResponse(value="Action processed successfully")


def create_agent(
    configuration: dict,
    connection_manager: Connections,
) -> AgentApplication:
    app = AgentApplication(
        storage=MemoryStorage(),
        connection_manager=connection_manager,
        **configuration,
    )
    TeamsAgentExtension(app)

    @app.activity(ActivityTypes.message)
    async def on_message(context: TurnContext, _state) -> None:
        text = (context.activity.text or "").strip().lower()
        if CARD_ACTIONS_COMMAND in text:
            await context.send_activity(
                Activity(
                    type=ActivityTypes.message,
                    attachments=[card_attachment(create_card_actions_card())],
                )
            )
        elif TOGGLE_VISIBILITY_COMMAND in text:
            await context.send_activity(
                Activity(
                    type=ActivityTypes.message,
                    attachments=[card_attachment(create_toggle_visibility_card())],
                )
            )
        else:
            await context.send_activity(HELP_TEXT)

    @app.adaptive_card.action_execute(SUBMIT_NAME_VERB)
    async def on_adaptive_card_action(context: TurnContext, state, action_data):
        return await handle_name_submission(context, state, action_data)

    return app

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
from pathlib import Path

from dotenv import load_dotenv
from microsoft_agents.activity import (
    Activity,
    Attachment,
    load_configuration_from_env,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    AgentApplication,
    Authorization,
    MemoryStorage,
    TurnContext,
    TurnState,
)
from microsoft_agents.hosting.msteams import TeamsAgentExtension


ROOT = Path(__file__).resolve().parent
load_dotenv(ROOT / ".env")


def _adaptive_card(content: dict) -> Attachment:
    return Attachment(
        content_type="application/vnd.microsoft.card.adaptive",
        content=content,
    )


def _invoke_data(context: TurnContext) -> dict:
    activity_value = context.activity.value or {}
    data = activity_value.get("data") or {}
    return data if isinstance(data, dict) else {}


def create_agent(dependencies: dict | None = None) -> AgentApplication:
    dependencies = dependencies or {}
    configuration = load_configuration_from_env(os.environ)
    connection_manager = dependencies.get("connection_manager")
    if connection_manager is None:
        connection_manager = MsalConnectionManager(**configuration)

    adapter = dependencies.get("adapter")
    if adapter is None:
        adapter = CloudAdapter(connection_manager=connection_manager)

    storage = MemoryStorage()
    authorization = Authorization(
        storage,
        connection_manager,
        **configuration,
    )
    app = AgentApplication[TurnState](
        storage=storage,
        adapter=adapter,
        authorization=authorization,
        **configuration,
    )
    teams = TeamsAgentExtension(app)
    endpoint = dependencies.get("endpoint") or os.getenv(
        "BOT_ENDPOINT", "http://localhost:3978"
    )
    @app.activity("message")
    async def on_message(context: TurnContext, _state: TurnState) -> None:
        card = {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.4",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Task Module Invocation from Adaptive Card",
                    "weight": "Bolder",
                    "size": "Large",
                }
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Adaptive Card",
                    "data": {
                        "msteams": {"type": "task/fetch"},
                        "verb": "adaptive_card",
                    },
                },
                {
                    "type": "Action.Submit",
                    "title": "Custom Form",
                    "data": {
                        "msteams": {"type": "task/fetch"},
                        "verb": "custom_form",
                    },
                },
                {
                    "type": "Action.Submit",
                    "title": "Multi-step Form",
                    "data": {
                        "msteams": {"type": "task/fetch"},
                        "verb": "multi_step_form",
                    },
                },
            ],
        }
        await context.send_activity(
            Activity(type="message", attachments=[_adaptive_card(card)])
        )

    @teams.task_modules.fetch("adaptive_card")
    async def fetch_adaptive_card(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        card = {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.0",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Enter Text Here",
                    "weight": "Bolder",
                },
                {
                    "type": "Input.Text",
                    "id": "usertext",
                    "placeholder": "add some text and submit",
                    "isMultiline": True,
                },
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Submit",
                    "data": {"verb": "adaptive_card_submit"},
                }
            ],
        }
        return {
            "task": {
                "type": "continue",
                "value": {
                    "title": "Adaptive Card: Inputs",
                    "width": 400,
                    "height": 200,
                    "card": {
                        "contentType": "application/vnd.microsoft.card.adaptive",
                        "content": card,
                    },
                },
            }
        }

    @teams.task_modules.fetch("custom_form")
    async def fetch_custom_form(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        return {
            "task": {
                "type": "continue",
                "value": {
                    "title": "Custom Form",
                    "width": 510,
                    "height": 450,
                    "url": f"{endpoint.rstrip('/')}/customform",
                    "fallbackUrl": f"{endpoint.rstrip('/')}/customform",
                },
            }
        }

    @teams.task_modules.fetch("multi_step_form")
    async def fetch_multi_step_form(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        card = {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.4",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Step 1 of 2 - Your Name",
                    "size": "Large",
                    "weight": "Bolder",
                },
                {
                    "type": "Input.Text",
                    "id": "name",
                    "label": "Name",
                    "placeholder": "Enter your name",
                    "isRequired": True,
                },
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Next",
                    "data": {"verb": "multi_step_1"},
                }
            ],
        }
        return {
            "task": {
                "type": "continue",
                "value": {
                    "title": "Multi-step Form",
                    "width": 400,
                    "height": 300,
                    "card": {
                        "contentType": "application/vnd.microsoft.card.adaptive",
                        "content": card,
                    },
                },
            }
        }

    @teams.task_modules.submit("adaptive_card_submit")
    async def submit_adaptive_card(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        user_text = _invoke_data(context).get("usertext")
        await context.send_activity(f"You submitted: {user_text}")
        return {
            "task": {
                "type": "message",
                "value": "Thanks for submitting!",
            }
        }

    @teams.task_modules.submit("custom_form_submit")
    async def submit_custom_form(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        data = _invoke_data(context)
        name = data.get("name")
        email = data.get("email")
        await context.send_activity(
            f"Hi {name}, thanks for submitting! Your email is {email}"
        )
        return {
            "task": {
                "type": "message",
                "value": "Form submitted successfully",
            }
        }

    @teams.task_modules.submit("multi_step_1")
    async def submit_multi_step_first(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        name = _invoke_data(context).get("name")
        card = {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.4",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Step 2 of 2 - Your Email",
                    "size": "Large",
                    "weight": "Bolder",
                },
                {
                    "type": "Input.Text",
                    "id": "email",
                    "label": "Email",
                    "placeholder": "Enter your email",
                    "isRequired": True,
                    "style": "email",
                },
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Submit",
                    "data": {"verb": "multi_step_2", "name": name},
                }
            ],
        }
        return {
            "task": {
                "type": "continue",
                "value": {
                    "title": "Multi-step Form: Step 2",
                    "width": 400,
                    "height": 300,
                    "card": {
                        "contentType": "application/vnd.microsoft.card.adaptive",
                        "content": card,
                    },
                },
            }
        }

    @teams.task_modules.submit("multi_step_2")
    async def submit_multi_step_second(
        context: TurnContext, _state: TurnState, _request: object
    ) -> dict:
        data = _invoke_data(context)
        name = data.get("name")
        email = data.get("email")
        await context.send_activity(
            f"Hi {name}, thanks for submitting! Your email is {email}"
        )
        return {
            "task": {
                "type": "message",
                "value": "Multi-step form completed!",
            }
        }

    @app.error
    async def on_error(context: TurnContext, error: Exception) -> None:
        print(f"Agent error: {error}")
        if context:
            print(f"Activity: {context.activity.type}")

    return app

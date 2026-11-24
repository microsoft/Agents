# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from typing import Any

from microsoft_agents.activity import Activity, CardAction, SuggestedActions
from microsoft_agents.hosting.core import TurnContext


def is_targeted(activity: Activity) -> bool:
    recipient = activity.recipient
    # Teams marks the recipient of a slash-command message with this extra field.
    return bool(recipient and recipient.properties.get("isTargeted"))


def _activity(payload: dict[str, Any]) -> Activity:
    return Activity.model_validate(payload)


def _targeted_payload(
    context: TurnContext, *, text: str | None = None, card: dict[str, Any] | None = None
) -> dict[str, Any]:
    activity = context.activity
    sender = activity.from_property
    recipient_id = (sender.id or "") if sender else ""
    recipient_name = (sender.name or "") if sender else ""
    conversation = activity.conversation
    shared = not conversation or conversation.conversation_type != "personal"
    entities: list[dict[str, Any]] = (
        [{"type": "activityTreatment", "treatment": "targeted"}] if shared else []
    )
    if shared and is_targeted(activity) and activity.id:
        entities.append(
            {"type": "targetedMessageInfo", "targetedMessageId": activity.id}
        )
    payload: dict[str, Any] = {
        "type": "message",
        "recipient": {"id": recipient_id, "name": recipient_name},
        "entities": entities,
    }
    if text is not None:
        payload["text"] = text
    if card is not None:
        payload["attachments"] = [
            {
                "contentType": "application/vnd.microsoft.card.adaptive",
                "content": card,
            }
        ]
    return payload


def _card(body: list[dict[str, Any]], actions: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": body,
        "actions": actions,
    }


def _action(title: str, verb: str, data: dict[str, Any]) -> dict[str, Any]:
    return {"type": "Action.Execute", "title": title, "verb": verb, "data": data}


def _format_span(seconds: float) -> str:
    total = round(seconds)
    if total >= 3600:
        return f"{total // 3600}h {(total % 3600) // 60}m"
    if total >= 60:
        return f"{total // 60}m {total % 60}s"
    return f"{total}s"


def _commands(user_id: str | None, entries: list[tuple[str, str]]) -> SuggestedActions:
    # Action.Submit chips raise an invoke without posting a visible message.
    actions = [
        CardAction.model_construct(
            type="Action.Submit", title=title, value={"command": command}
        )
        for title, command in entries
    ]
    return SuggestedActions.model_construct(
        to=[user_id] if user_id else [], actions=actions
    )


async def _send_invoke_response(
    context: TurnContext, body: dict[str, Any] | None = None
) -> None:
    await context.send_activity(
        _activity(
            {
                "type": "invokeResponse",
                "value": {"status": 200, **({"body": body} if body else {})},
            }
        )
    )

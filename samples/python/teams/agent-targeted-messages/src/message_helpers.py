# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from typing import Any

from microsoft_agents.activity import Activity, CardAction, SuggestedActions


_MISSING = object()


def _read(value: Any, name: str, default: Any = None) -> Any:
    """Read a field from a dict or SDK model, including camelCase extra fields."""
    if value is None:
        return default
    if isinstance(value, dict):
        return value.get(name, default)
    found = getattr(value, name, _MISSING)
    if found is not _MISSING and found is not None:
        return found
    if hasattr(value, "model_dump"):
        dumped = value.model_dump(by_alias=True)
        parts = name.split("_")
        camel = parts[0] + "".join(part.title() for part in parts[1:])
        return dumped.get(camel, dumped.get(name, default))
    return default


def _activity(payload: dict[str, Any]) -> Activity:
    return Activity.model_validate(payload)


def _targeted_payload(
    context: Any, *, text: str | None = None, card: dict[str, Any] | None = None
) -> dict[str, Any]:
    activity = context.activity
    sender = _read(activity, "from_property")
    recipient_id = _read(sender, "id", "")
    recipient_name = _read(sender, "name", "")
    shared = _read(_read(activity, "conversation"), "conversation_type", "") != "personal"
    entities: list[dict[str, Any]] = (
        [{"type": "activityTreatment", "treatment": "targeted"}] if shared else []
    )
    incoming_id = _read(activity, "id")
    if shared and _read(_read(activity, "recipient"), "is_targeted", False) and incoming_id:
        entities.append(
            {"type": "targetedMessageInfo", "targetedMessageId": incoming_id}
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
    context: Any, body: dict[str, Any] | None = None
) -> None:
    await context.send_activity(
        _activity(
            {
                "type": "invokeResponse",
                "value": {"status": 200, **({"body": body} if body else {})},
            }
        )
    )

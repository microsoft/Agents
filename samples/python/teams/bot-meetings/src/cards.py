# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Adaptive card payloads for meeting event responses."""

from microsoft_agents.activity import Activity, ActivityTypes, Attachment


def meeting_card(
    body: list[dict], actions: list[dict] | None = None
) -> Activity:
    content = {
        "type": "AdaptiveCard",
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "version": "1.5",
        "body": body,
    }
    if actions:
        content["actions"] = actions
    attachment = Attachment(
        content_type="application/vnd.microsoft.card.adaptive",
        content=content,
    )
    return Activity(type=ActivityTypes.message, attachments=[attachment])

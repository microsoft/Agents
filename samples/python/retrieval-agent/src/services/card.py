# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import annotations

from .retrieval_client import RetrievalItem


def create_source_card(items: list[RetrievalItem]) -> dict:
    return {
        "type": "AdaptiveCard",
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "version": "1.5",
        "body": [
            {
                "type": "Container",
                "items": [
                    {"type": "TextBlock", "text": item.title, "weight": "Bolder", "wrap": True},
                    {"type": "TextBlock", "text": item.extract, "wrap": True, "spacing": "Small"},
                ],
                "selectAction": {"type": "Action.OpenUrl", "title": "Open source", "url": item.web_url},
            }
            for item in items
        ],
    }

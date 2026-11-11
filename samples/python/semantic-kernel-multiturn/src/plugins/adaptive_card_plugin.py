# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from typing import Any
from urllib.parse import quote

from semantic_kernel.functions import kernel_function


class AdaptiveCardPlugin:
    @kernel_function(
        name="get_adaptive_card_for_data",
        description="Create an Adaptive Card 1.5 for weather forecast data.",
    )
    def get_adaptive_card_for_data(
        self,
        location: str,
        date: str,
        temperature_c: int,
        temperature_f: int,
    ) -> dict[str, Any]:
        return {
            "type": "AdaptiveCard",
            "version": "1.5",
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "body": [
                {
                    "type": "TextBlock",
                    "text": f"Weather forecast for {location}",
                    "weight": "Bolder",
                    "size": "Medium",
                    "wrap": True,
                },
                {
                    "type": "FactSet",
                    "facts": [
                        {"title": "Date", "value": date},
                        {
                            "title": "Temperature",
                            "value": f"{temperature_c} C / {temperature_f} F",
                        },
                    ],
                },
            ],
            "actions": [
                {
                    "type": "Action.OpenUrl",
                    "title": "More details",
                    "url": (
                        "https://www.msn.com/en-us/weather/forecast/in-"
                        f"{quote(location, safe='')}"
                    ),
                }
            ],
        }

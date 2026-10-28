# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from datetime import datetime
import random
from typing import Annotated

from semantic_kernel.functions import kernel_function

from .progress import report_progress
from .weather_forecast import WeatherForecast


def _display_date(value: str) -> str:
    try:
        return datetime.fromisoformat(value).strftime("%B %d, %Y")
    except ValueError:
        return value


class WeatherForecastPlugin:
    @kernel_function(
        name="get_forecast_for_date",
        description="Get a synthetic weather forecast for a specific date and location.",
    )
    async def get_forecast_for_date(
        self,
        date: Annotated[str, "The date for the forecast, for example 2026-09-19"],
        location: Annotated[str, "The location for the forecast, for example Seattle, WA"],
    ) -> WeatherForecast:
        await report_progress(
            f"Looking up the weather in {location} for {_display_date(date)}"
        )

        temperature_c = random.randrange(-20, 55)
        temperature_f = round((temperature_c * 9 / 5) + 32)
        return WeatherForecast(
            date=date,
            location=location,
            temperatureC=temperature_c,
            temperatureF=temperature_f,
        )

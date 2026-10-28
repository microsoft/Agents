# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging
import os
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from typing import Annotated, Any, Callable

import aiohttp
from agent_framework import tool
from pydantic import Field

from .progress import report_progress

logger = logging.getLogger(__name__)

OPENWEATHER_BASE = "https://api.openweathermap.org"


def _location_query(location: str, state: str) -> str:
    return ",".join(part for part in (location, state) if part)


def _temperature(values: list[float], reducer: Callable[[list[float]], float]) -> str:
    if not values:
        return "N/A"
    return f"{reducer(values):g}°F"


def _local_datetime(item: dict[str, Any], utc_offset_seconds: int) -> datetime | None:
    try:
        timestamp = int(item["dt"])
    except (KeyError, TypeError, ValueError):
        return None

    local_timezone = timezone(timedelta(seconds=utc_offset_seconds))
    return datetime.fromtimestamp(timestamp, tz=timezone.utc).astimezone(local_timezone)


def _distance_from_noon(date_time: datetime) -> float:
    return abs((date_time.time().hour * 60) + date_time.time().minute - (12 * 60))


@tool(approval_mode="never_require")
async def get_current_weather(
    location: Annotated[str, Field(description="The city name to look up weather for")],
    state: Annotated[
        str, Field(description="The US state name or code, if applicable")
    ],
) -> str:
    """Retrieves the current weather for a location. Location is a city name."""
    api_key = os.environ["OPEN_WEATHER_API_KEY"]
    query = _location_query(location, state)

    await report_progress(f"Looking up the Current Weather in {location}")

    url = f"{OPENWEATHER_BASE}/data/2.5/weather"
    params = {"q": query, "appid": api_key, "units": "imperial"}

    await report_progress(f"Fetching Current Weather for {location}")
    async with aiohttp.ClientSession() as session:
        async with session.get(url, params=params) as response:
            if response.status != 200:
                logger.error("Weather API failed with status %s", response.status)
                return "Failed to retrieve weather data."
            data = await response.json()

    main = data.get("main", {})
    weather = data.get("weather", [{}])[0]
    wind = data.get("wind", {})

    return (
        f"Current weather in {query}:\n"
        f"Temperature: {main.get('temp')}°F\n"
        f"Low: {main.get('temp_min')}°F, High: {main.get('temp_max')}°F\n"
        f"Humidity: {main.get('humidity')}%\n"
        f"Wind: {wind.get('speed')} mph\n"
        f"Conditions: {weather.get('description', 'N/A')}"
    )


@tool(approval_mode="never_require")
async def get_weather_forecast(
    location: Annotated[
        str, Field(description="The city name to look up the forecast for")
    ],
    state: Annotated[
        str, Field(description="The US state name or code, if applicable")
    ],
) -> str:
    """Retrieves the 5-day weather forecast for a location. Location is a city name."""
    api_key = os.environ["OPEN_WEATHER_API_KEY"]
    query = _location_query(location, state)

    await report_progress(f"Looking up the Weather Forecast in {location}")

    url = f"{OPENWEATHER_BASE}/data/2.5/forecast"
    params = {"q": query, "appid": api_key, "units": "imperial"}

    await report_progress(f"Fetching Weather Forecast for {location}")
    async with aiohttp.ClientSession() as session:
        async with session.get(url, params=params) as response:
            if response.status != 200:
                logger.error("Forecast API failed with status %s", response.status)
                return "Failed to retrieve forecast data."
            data = await response.json()

    items = data.get("list", [])
    if not items:
        return "No forecast data available."

    timezone_offset = data.get("city", {}).get("timezone", 0)
    if not isinstance(timezone_offset, int):
        timezone_offset = 0

    days: dict[str, list[tuple[dict[str, Any], datetime]]] = defaultdict(list)
    for item in items:
        date_time = _local_datetime(item, timezone_offset)
        if date_time is not None:
            days[date_time.date().isoformat()].append((item, date_time))

    lines = [f"5-day forecast for {query}:\n"]
    for date, entries in sorted(days.items())[:5]:
        high_values = [
            value
            for item, _ in entries
            if isinstance((value := item.get("main", {}).get("temp_max")), (int, float))
        ]
        low_values = [
            value
            for item, _ in entries
            if isinstance((value := item.get("main", {}).get("temp_min")), (int, float))
        ]
        representative, _ = min(
            entries,
            key=lambda entry: _distance_from_noon(entry[1]),
        )
        weather = representative.get("weather", [{}])[0]
        lines.append(
            f"  {date}: High {_temperature(high_values, max)}, "
            f"Low {_temperature(low_values, min)} — "
            f"{weather.get('description', 'N/A')}"
        )

    return "\n".join(lines)

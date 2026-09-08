# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
import logging
from typing import Annotated

import aiohttp
from agent_framework import tool
from pydantic import Field

logger = logging.getLogger(__name__)

OPENWEATHER_BASE = "https://api.openweathermap.org"

@tool(approval_mode="never_require")
async def get_current_weather(
    city: Annotated[str, Field(description="The city name to look up weather for")],
    state: Annotated[str, Field(description="The US state code, if applicable")],
    country: Annotated[str, Field(description="The country code, if applicable")] = "US"
) -> str:
    """Retrieves the current weather for a location. Location is a city name."""
    api_key = os.environ.get("OPEN_WEATHER_API_KEY", "")
    if not api_key:
        return "Error: OPEN_WEATHER_API_KEY is not configured."

    url = f"{OPENWEATHER_BASE}/data/2.5/weather"
    params = {"q": f"{city},{state},{country}", "appid": api_key, "units": "imperial"}
    print(f"Requesting current weather for {city}, {state}, {country} from {url} with params {params}")

    async with aiohttp.ClientSession() as session:
        async with session.get(url, params=params) as resp:
            if resp.status != 200:
                logger.error("Weather API failed: %s", await resp.text())
                return "Failed to retrieve weather data."
            data = await resp.json()

    main = data.get("main", {})
    weather = data.get("weather", [{}])[0]
    wind = data.get("wind", {})

    return (
        f"Current weather in {city}, {state}, {country}:\n"
        f"Temperature: {main.get('temp')}°F\n"
        f"Low: {main.get('temp_min')}°F, High: {main.get('temp_max')}°F\n"
        f"Humidity: {main.get('humidity')}%\n"
        f"Wind: {wind.get('speed')} mph\n"
        f"Conditions: {weather.get('description', 'N/A')}"
    )


@tool(approval_mode="never_require")
async def get_weather_forecast(
    city: Annotated[str, Field(description="The city name to look up the forecast for")],
    state: Annotated[str, Field(description="The US state name or code, if applicable")],
    country: Annotated[str, Field(description="The country code, if applicable")] = "US"
) -> str:
    """Retrieves the 5-day weather forecast for a location. Location is a city name."""
    api_key = os.environ.get("OPEN_WEATHER_API_KEY", "")
    if not api_key:
        return "Error: OPEN_WEATHER_API_KEY is not configured."

    url = f"{OPENWEATHER_BASE}/data/2.5/forecast"
    params = {"q": f"{city},{state},{country}", "appid": api_key, "units": "imperial"}
    print(f"Requesting weather forecast for {city}, {state} from {url} with params {params}")

    async with aiohttp.ClientSession() as session:
        async with session.get(url, params=params) as resp:
            if resp.status != 200:
                logger.error("Forecast API failed: %s", await resp.text())
                return "Failed to retrieve forecast data."
            data = await resp.json()

    items = data.get("list", [])
    if not items:
        return "No forecast data available."

    # Group by day, pick one entry per day (noon or first available)
    days: dict[str, dict] = {}
    for item in items:
        date_str = item["dt_txt"].split(" ")[0]
        if date_str not in days:
            days[date_str] = item
        elif "12:00:00" in item["dt_txt"]:
            days[date_str] = item

    lines = [f"5-day forecast for {city}, {state}, {country}:\n"]
    for date_str, item in list(days.items())[:5]:
        main = item.get("main", {})
        weather = item.get("weather", [{}])[0]
        lines.append(
            f"  {date_str}: High {main.get('temp_max')}°F, Low {main.get('temp_min')}°F "
            f"— {weather.get('description', 'N/A')}"
        )

    return "\n".join(lines)

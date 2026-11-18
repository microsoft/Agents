# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging
from collections.abc import Mapping
from typing import Any
from urllib.parse import urlencode

import httpx

logger = logging.getLogger(__name__)
WIKIPEDIA_API = "https://en.wikipedia.org/w/api.php"
USER_AGENT = "BotMessageExtensions/1.0 (Teams agent; +https://example.com)"


async def search_wikipedia(query: str) -> list[dict[str, Any]]:
    if not query:
        return []

    parameters = {
        "action": "query",
        "format": "json",
        "list": "search",
        "srsearch": query,
        "utf8": "1",
        "srlimit": "8",
    }
    url = f"{WIKIPEDIA_API}?{urlencode(parameters)}"
    try:
        async with httpx.AsyncClient(timeout=10) as client:
            response = await client.get(url, headers={"User-Agent": USER_AGENT})
            response.raise_for_status()
            payload = response.json()
    except (httpx.HTTPError, ValueError) as error:
        logger.warning("Wikipedia search failed: %s", error)
        return []

    if not isinstance(payload, Mapping):
        logger.warning("Wikipedia search returned an unexpected response shape")
        return []

    query_data = payload.get("query")
    if not isinstance(query_data, Mapping):
        logger.warning("Wikipedia search returned an unexpected query shape")
        return []

    results = query_data.get("search")
    if not isinstance(results, list):
        logger.warning("Wikipedia search returned an unexpected response shape")
        return []
    return [result for result in results if isinstance(result, dict)]

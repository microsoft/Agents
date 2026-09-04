# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass
from enum import Enum
from os import environ
from typing import Awaitable, Callable
from urllib.parse import urlparse

import aiohttp

logger = logging.getLogger(__name__)


class RetrievalStatus(str, Enum):
    SUCCESS = "success"
    NOT_SIGNED_IN = "not_signed_in"
    NO_RESULTS = "no_results"
    SERVICE_UNAVAILABLE = "service_unavailable"


@dataclass(frozen=True)
class RetrievalItem:
    title: str
    extract: str
    web_url: str


@dataclass(frozen=True)
class RetrievalResult:
    status: RetrievalStatus
    items: list[RetrievalItem]


@dataclass(frozen=True)
class RetrievalOptions:
    sharepoint_site_url: str
    maximum_number_of_results: int


GetAccessToken = Callable[[], Awaitable[str | None]]
PostRetrievalRequest = Callable[[str, dict], Awaitable[tuple[int, dict]]]


def get_retrieval_options(environment: dict[str, str] = environ) -> RetrievalOptions:
    site_url = environment.get("RETRIEVAL_SHAREPOINT_SITE_URL", "")
    parsed_url = urlparse(site_url)
    if parsed_url.scheme != "https" or not parsed_url.netloc.endswith(".sharepoint.com"):
        raise ValueError(
            "RETRIEVAL_SHAREPOINT_SITE_URL must be an absolute HTTPS SharePoint site URL."
        )

    try:
        maximum_number_of_results = int(
            environment.get("RETRIEVAL_MAXIMUM_NUMBER_OF_RESULTS", "3")
        )
    except ValueError as exception:
        raise ValueError(
            "RETRIEVAL_MAXIMUM_NUMBER_OF_RESULTS must be from 1 through 25."
        ) from exception
    if not 1 <= maximum_number_of_results <= 25:
        raise ValueError("RETRIEVAL_MAXIMUM_NUMBER_OF_RESULTS must be from 1 through 25.")

    return RetrievalOptions(site_url, maximum_number_of_results)


def create_filter_expression(sharepoint_site_url: str) -> str:
    return f'path:"{sharepoint_site_url.rstrip("/")}/"'


async def retrieve_sharepoint(
    question: str,
    get_access_token: GetAccessToken,
    options: RetrievalOptions | None = None,
    post_request: PostRetrievalRequest | None = None,
) -> RetrievalResult:
    if not question.strip():
        return RetrievalResult(RetrievalStatus.NO_RESULTS, [])

    try:
        access_token = await get_access_token()
    except asyncio.CancelledError:
        raise
    except Exception:
        logger.warning("The delegated Microsoft Graph token was not available.")
        return RetrievalResult(RetrievalStatus.NOT_SIGNED_IN, [])
    if not access_token:
        return RetrievalResult(RetrievalStatus.NOT_SIGNED_IN, [])

    options = options or get_retrieval_options()
    body = {
        "queryString": question,
        # SharePoint is the default data source. See the README for the OneDrive for Business fallback.
        "dataSource": "sharePoint",
        "filterExpression": create_filter_expression(options.sharepoint_site_url),
        "resourceMetadata": ["title", "author"],
        "maximumNumberOfResults": options.maximum_number_of_results,
    }
    try:
        status, payload = await (post_request or _post_retrieval_request)(access_token, body)
        if not 200 <= status < 300:
            logger.warning("Copilot Retrieval API returned status code %s.", status)
            return RetrievalResult(RetrievalStatus.SERVICE_UNAVAILABLE, [])
        items = map_retrieval_items(payload)
        return (
            RetrievalResult(RetrievalStatus.SUCCESS, items)
            if items
            else RetrievalResult(RetrievalStatus.NO_RESULTS, [])
        )
    except asyncio.CancelledError:
        raise
    except Exception:
        logger.error("Copilot Retrieval API request failed.")
        return RetrievalResult(RetrievalStatus.SERVICE_UNAVAILABLE, [])


async def _post_retrieval_request(access_token: str, body: dict) -> tuple[int, dict]:
    async with aiohttp.ClientSession() as session:
        async with session.post(
            "https://graph.microsoft.com/v1.0/copilot/retrieval",
            headers={"Authorization": f"Bearer {access_token}"},
            json=body,
        ) as response:
            if not 200 <= response.status < 300:
                return response.status, {}
            return response.status, await response.json()


def map_retrieval_items(payload: dict) -> list[RetrievalItem]:
    items: list[RetrievalItem] = []
    for hit in payload.get("retrievalHits", []):
        web_url = hit.get("webUrl")
        extract = next(
            (
                item.get("text", "").strip()
                for item in hit.get("extracts", [])
                if item.get("text", "").strip()
            ),
            None,
        )
        if not web_url or not extract:
            continue
        title = hit.get("resourceMetadata", {}).get("title", "").strip()
        items.append(RetrievalItem(title or "Build session information", extract, web_url))
    return items

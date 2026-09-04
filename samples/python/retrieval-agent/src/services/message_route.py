# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import annotations

from typing import Awaitable, Callable

from .card import create_source_card
from .retrieval_client import RetrievalResult, RetrievalStatus

BUILD_GENIE_RESPONSES = {
    RetrievalStatus.NOT_SIGNED_IN: "Please sign in to Microsoft 365, then ask your Build question again.",
    RetrievalStatus.NO_RESULTS: "I couldn't find Build session information in the configured SharePoint site. Check the site URL, document permissions, and indexing, then try a more specific question.",
    RetrievalStatus.SERVICE_UNAVAILABLE: "I couldn't retrieve Build session information right now. Please try again later.",
}


def grounded_answer(result: RetrievalResult) -> str:
    sources = "\n\n".join(
        f"{item.title}\n{item.extract}\nSource: {item.web_url}" for item in result.items
    )
    return f"Here is what I found in the configured SharePoint site:\n\n{sources}"


async def handle_build_genie_message(
    result: RetrievalResult,
    send_text: Callable[[str], Awaitable[object]],
    send_source_card: Callable[[dict], Awaitable[object]],
) -> None:
    if result.status is not RetrievalStatus.SUCCESS:
        await send_text(BUILD_GENIE_RESPONSES[result.status])
        return
    await send_text(grounded_answer(result))
    await send_source_card(create_source_card(result.items))

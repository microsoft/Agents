# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio
import unittest

from src.services.message_route import BUILD_GENIE_RESPONSES, handle_build_genie_message
from src.services.retrieval_client import (
    RetrievalItem,
    RetrievalOptions,
    RetrievalResult,
    RetrievalStatus,
    create_filter_expression,
    get_retrieval_options,
    retrieve_sharepoint,
)


OPTIONS = RetrievalOptions("https://contoso.sharepoint.com/sites/Build", 3)


class RetrievalTests(unittest.IsolatedAsyncioTestCase):
    def test_creates_configured_site_filter(self):
        self.assertEqual(
            create_filter_expression(OPTIONS.sharepoint_site_url),
            'path:"https://contoso.sharepoint.com/sites/Build/"',
        )
        with self.assertRaises(ValueError):
            get_retrieval_options({"RETRIEVAL_SHAREPOINT_SITE_URL": "http://contoso.sharepoint.com/sites/Build"})

    async def test_retrieves_mapped_items_with_a_delegated_token(self):
        captured: dict = {}

        async def post_request(token: str, body: dict):
            captured["token"] = token
            captured["body"] = body
            return 200, {"retrievalHits": [{"webUrl": "https://contoso.sharepoint.com/session.docx", "extracts": [{"text": "Session details"}], "resourceMetadata": {"title": "Pricing Analytics"}}]}

        result = await retrieve_sharepoint("Pricing Analytics", lambda: _token("delegated-token"), OPTIONS, post_request)
        self.assertEqual(result.status, RetrievalStatus.SUCCESS)
        self.assertEqual(result.items[0].title, "Pricing Analytics")
        self.assertEqual(captured["token"], "delegated-token")
        self.assertEqual(captured["body"]["dataSource"], "sharePoint")

    async def test_maps_token_and_service_failures_to_safe_statuses(self):
        async def no_token():
            raise ValueError("secret")

        async def failed_request(_token: str, _body: dict):
            return 502, {}

        self.assertEqual((await retrieve_sharepoint("Build", no_token, OPTIONS)).status, RetrievalStatus.NOT_SIGNED_IN)
        self.assertEqual((await retrieve_sharepoint("Build", lambda: _token("token"), OPTIONS, failed_request)).status, RetrievalStatus.SERVICE_UNAVAILABLE)
        self.assertEqual((await retrieve_sharepoint("Build", lambda: _token("token"), OPTIONS, lambda _token, _body: _response(200, {"retrievalHits": []}))).status, RetrievalStatus.NO_RESULTS)

    async def test_propagates_cancellation(self):
        async def cancelled_token():
            raise asyncio.CancelledError

        with self.assertRaises(asyncio.CancelledError):
            await retrieve_sharepoint("Build", cancelled_token, OPTIONS)

        async def cancelled_request(_token: str, _body: dict):
            raise asyncio.CancelledError

        with self.assertRaises(asyncio.CancelledError):
            await retrieve_sharepoint(
                "Build", lambda: _token("token"), OPTIONS, cancelled_request
            )

    async def test_message_route_sends_grounded_text_and_source_card(self):
        messages: list[str] = []
        cards: list[dict] = []
        result = RetrievalResult(RetrievalStatus.SUCCESS, [RetrievalItem("Pricing Analytics", "Session details", "https://contoso.sharepoint.com/session.docx")])
        await handle_build_genie_message(result, lambda text: _append(messages, text), lambda card: _append(cards, card))
        self.assertIn("https://contoso.sharepoint.com/session.docx", messages[0])
        self.assertEqual(len(cards), 1)
        self.assertIn("https://contoso.sharepoint.com/session.docx", str(cards[0]))

    async def test_message_route_sends_safe_messages_for_failures(self):
        for status in (RetrievalStatus.NOT_SIGNED_IN, RetrievalStatus.NO_RESULTS, RetrievalStatus.SERVICE_UNAVAILABLE):
            messages: list[str] = []
            cards: list[dict] = []
            await handle_build_genie_message(RetrievalResult(status, []), lambda text: _append(messages, text), lambda card: _append(cards, card))
            self.assertEqual(messages, [BUILD_GENIE_RESPONSES[status]])
            self.assertEqual(cards, [])


async def _token(value: str) -> str:
    return value


async def _append(items: list, value):
    items.append(value)


async def _response(status: int, payload: dict):
    return status, payload

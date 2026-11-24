# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Supplementary attachment failure coverage."""

import unittest
from unittest.mock import AsyncMock

from microsoft_agents.activity import Activity, Attachment, ResourceResponse
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import TurnContext

from src.agent import (
    CONTENT_TYPE_FILE_DOWNLOAD,
    _download_attachment_or_none,
    create_agent,
)


class OfflineConnections:
    def get_token_provider(self, *args, **kwargs):
        raise AssertionError("Unexpected credential request in offline test")


class CapturingAdapter(CloudAdapter):
    def __init__(self):
        super().__init__(connection_manager=OfflineConnections())
        self.sent = []

    async def send_activities(self, context, activities):
        self.sent.extend(
            activity.model_dump(mode="json", by_alias=True, exclude_none=True)
            for activity in activities
        )
        return [ResourceResponse(id=str(index)) for index, _ in enumerate(activities)]


class AttachmentFailureTests(unittest.IsolatedAsyncioTestCase):
    async def test_failed_download_returns_empty_content_for_welcome_fallback(self):
        downloader = AsyncMock(side_effect=RuntimeError("download failed"))
        attachment = Attachment(
            content_type=CONTENT_TYPE_FILE_DOWNLOAD,
            content={"downloadUrl": "https://example.invalid/download"},
        )

        content = await _download_attachment_or_none(attachment, downloader)

        self.assertIsNone(content)
        downloader.assert_awaited_once_with("https://example.invalid/download")

    async def test_failed_download_sends_agent_welcome_through_sdk_dispatch(self):
        downloader = AsyncMock(side_effect=RuntimeError("download failed"))
        connection_manager = OfflineConnections()
        adapter = CapturingAdapter()
        app = create_agent({
            "connection_manager": connection_manager,
            "adapter": adapter,
            "download": downloader,
        })
        activity = Activity.model_validate({
            "type": "message",
            "id": "failed-download",
            "channelId": "msteams",
            "serviceUrl": "https://example.invalid",
            "conversation": {"id": "conversation-1", "conversationType": "personal"},
            "from": {"id": "user-1", "name": "Alice"},
            "recipient": {"id": "agent-1", "name": "Sample Agent"},
            "attachments": [{
                "contentType": CONTENT_TYPE_FILE_DOWNLOAD,
                "content": {"downloadUrl": "https://example.invalid/download"},
            }],
        })
        context = TurnContext(adapter, activity)
        try:
            await app.on_turn(context)
        finally:
            await context._aclose()

        self.assertEqual(
            adapter.sent[0]["text"],
            "Welcome to the Agent Attachments sample! Please attach a file or "
            "image to save to your OneDrive.",
        )
        downloader.assert_awaited_once_with("https://example.invalid/download")

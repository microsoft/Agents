# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""File consent and asynchronous upload behavior through SDK dispatch."""

import asyncio
import unittest
from unittest.mock import AsyncMock

from harness import APPLICATION_ADAPTERS, dispatch, file_transfer_server, invoke_response, sample_application


async def request_consent(app):
    sent = await dispatch(app, {"attachments": [{
        "contentType": "application/vnd.microsoft.teams.file.download.info",
        "name": "notes.txt", "content": {"downloadUrl": "https://example.invalid/download"},
    }]})
    card = next(item["attachments"][0] for item in sent if item.get("attachments"))
    if card["contentType"] != "application/vnd.microsoft.teams.card.file.consent":
        raise AssertionError("Expected file consent attachment")
    if card["content"]["sizeInBytes"] != 5 or card["name"] != "notes.txt":
        raise AssertionError("Consent must preserve downloaded size and filename")
    if card["content"]["acceptContext"] != card["content"]["declineContext"]:
        raise AssertionError("Consent accept and decline contexts must identify the same file")
    return card["content"]["acceptContext"]


def consent(context, action="accept"):
    return {
        "type": "invoke", "name": "fileConsent/invoke",
        "value": {"action": action, "context": context, "uploadInfo": {
            "uploadUrl": "https://example.invalid/upload", "name": "notes.txt",
            "uniqueId": "file-1", "fileType": "txt", "contentUrl": "https://example.invalid/file",
        }},
    }


class AttachmentContracts(unittest.IsolatedAsyncioTestCase):
    async def test_default_transfer_helpers_preserve_bytes_headers_and_status_handling(self):
        from src.files import download_attachment, upload_to_onedrive

        with file_transfer_server() as (url, requests):
            content = await download_attachment(url + "/download")
            self.assertEqual(content, b"notes")
            await upload_to_onedrive(url + "/upload", content)
        self.assertEqual(requests[0]["content"], b"notes")
        headers = {name.lower(): value for name, value in requests[0]["headers"].items()}
        self.assertEqual(headers["content-type"], "application/octet-stream")
        self.assertEqual(headers["content-length"], "5")
        self.assertEqual(headers["content-range"], "bytes 0-4/5")
        with file_transfer_server(get_status=500, put_status=500) as (url, _):
            with self.assertRaises(Exception):
                await download_attachment(url + "/download")
            with self.assertRaises(Exception):
                await upload_to_onedrive(url + "/upload", b"notes")

    async def test_upload_preserves_bytes_and_returns_file_info_proactively(self):
        download = AsyncMock(return_value=b"notes")
        upload = AsyncMock()
        app = sample_application({"download": download, "upload": upload})
        context = await request_consent(app)
        download.assert_awaited_once_with("https://example.invalid/download")
        sent = await dispatch(app, consent(context))
        self.assertEqual(invoke_response(sent)["status"], 200)
        await app.wait_for_uploads()
        upload.assert_awaited_once_with("https://example.invalid/upload", b"notes")
        self.assertEqual(len(APPLICATION_ADAPTERS[app].continued), 1)
        file = next(item["attachments"][0] for item in sent if item.get("attachments"))
        self.assertEqual(file["contentType"], "application/vnd.microsoft.teams.card.file.info")
        self.assertEqual(file["content"]["uniqueId"], "file-1")
        self.assertEqual(file["contentUrl"], "https://example.invalid/file")
        self.assertEqual(file["name"], "notes.txt")
        await dispatch(app, consent(context))
        await app.wait_for_uploads()
        self.assertEqual(upload.await_count, 1)

    async def test_acceptance_acknowledges_before_slow_upload_completes(self):
        started = asyncio.Event()
        release = asyncio.Event()

        async def upload(url, content):
            started.set()
            await release.wait()

        app = sample_application({"download": AsyncMock(return_value=b"notes"), "upload": upload})
        context = await request_consent(app)
        try:
            sent = await asyncio.wait_for(dispatch(app, consent(context)), timeout=2)
            self.assertEqual(invoke_response(sent)["status"], 200)
            await asyncio.wait_for(started.wait(), timeout=2)
            self.assertFalse(any(
                item.get("attachments", [{}])[0].get("contentType") == "application/vnd.microsoft.teams.card.file.info"
                for item in sent
            ))
        finally:
            release.set()
            await app.wait_for_uploads()

    async def test_decline_clears_pending_data_and_help_uses_agent_terminology(self):
        upload = AsyncMock()
        app = sample_application({"download": AsyncMock(return_value=b"notes"), "upload": upload})
        context = await request_consent(app)
        sent = await dispatch(app, consent(context, "decline"))
        self.assertEqual(invoke_response(sent)["status"], 200)
        self.assertTrue(any("Declined" in item.get("text", "") for item in sent))
        await dispatch(app, consent(context))
        await app.wait_for_uploads()
        upload.assert_not_awaited()
        help_messages = await dispatch(app, {"text": "hello"})
        self.assertIn("attach", help_messages[0]["text"].lower())
        self.assertNotIn("Bot Attachments", help_messages[0]["text"])

    async def test_failed_upload_cannot_reuse_pending_bytes(self):
        upload = AsyncMock(side_effect=RuntimeError("transfer failed"))
        app = sample_application({"download": AsyncMock(return_value=b"notes"), "upload": upload})
        context = await request_consent(app)
        await dispatch(app, consent(context))
        await app.wait_for_uploads()
        await dispatch(app, consent(context))
        await app.wait_for_uploads()
        self.assertEqual(upload.await_count, 1)

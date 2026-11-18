# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Teams attachment consent and upload handlers for the Agents SDK."""

import asyncio
import logging
from collections.abc import Awaitable, Callable
from os import environ
from typing import Any
from uuid import uuid4

from dotenv import load_dotenv
from microsoft_agents.activity import (
    Activity,
    Attachment,
    load_configuration_from_env,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import AgentApplication, MemoryStorage, TurnContext
from microsoft_agents.hosting.msteams import TeamsAgentExtension

from .files import download_attachment, upload_to_onedrive


logger = logging.getLogger(__name__)
CONTENT_TYPE_FILE_DOWNLOAD = "application/vnd.microsoft.teams.file.download.info"
CONTENT_TYPE_FILE_CONSENT = "application/vnd.microsoft.teams.card.file.consent"
CONTENT_TYPE_FILE_INFO = "application/vnd.microsoft.teams.card.file.info"


def _mapping(value: Any) -> dict[str, Any]:
    if isinstance(value, dict):
        return value
    model_dump = getattr(value, "model_dump", None)
    if callable(model_dump):
        dumped = model_dump(mode="python", by_alias=False, exclude_none=True)
        if isinstance(dumped, dict):
            return dumped
    return {}


def _field(mapping: dict[str, Any], *names: str) -> Any:
    for name in names:
        if name in mapping:
            return mapping[name]
    return None


async def _download_attachment_or_none(
    attachment: Attachment | None,
    downloader: Callable[[str], Awaitable[bytes]],
) -> bytes | None:
    if attachment is None or attachment.content_type != CONTENT_TYPE_FILE_DOWNLOAD:
        return None

    content = _mapping(attachment.content)
    url = content.get("downloadUrl") or content.get("download_url")
    if not isinstance(url, str) or not url:
        logger.warning("Incoming file attachment did not include a download URL")
        return None
    try:
        return await downloader(url)
    except Exception:
        logger.exception("Failed to download incoming attachment")
        return None


def create_agent(dependencies: dict[str, Any] | None = None) -> AgentApplication:
    """Create one isolated application instance with optional offline services."""
    load_dotenv()
    supplied = dependencies or {}
    connection_manager = supplied.get("connection_manager")
    configuration = load_configuration_from_env(environ)
    if connection_manager is None:
        connection_manager = MsalConnectionManager(**configuration)

    adapter = supplied.get("adapter")
    if adapter is None:
        adapter = CloudAdapter(connection_manager=connection_manager)

    download = supplied.get("download", download_attachment)
    upload = supplied.get("upload", upload_to_onedrive)
    app = AgentApplication(
        storage=MemoryStorage(),
        adapter=adapter,
        connection_manager=connection_manager,
    )
    teams = TeamsAgentExtension(app)
    pending_uploads: dict[str, bytes] = {}
    upload_tasks: set[asyncio.Task[None]] = set()

    async def get_attachment_content(context: TurnContext) -> bytes | None:
        attachment = next(iter(context.activity.attachments or []), None)
        return await _download_attachment_or_none(attachment, download)

    async def send_file_consent(
        context: TurnContext, filename: str, file_id: str
    ) -> None:
        consent_context = {"filename": filename, "file_id": file_id}
        attachment = Attachment(
            content_type=CONTENT_TYPE_FILE_CONSENT,
            name=filename,
            content={
                "description": "This is the file I want to send you",
                "sizeInBytes": len(pending_uploads[file_id]),
                "acceptContext": consent_context,
                "declineContext": consent_context,
            },
        )
        await context.send_activity(Activity(type="message", attachments=[attachment]))

    async def upload_in_continuation(
        context: TurnContext, upload_info: dict[str, Any], content: bytes
    ) -> None:
        url = _field(upload_info, "upload_url", "uploadUrl")
        if not isinstance(url, str) or not url:
            raise ValueError("File consent did not include an upload URL")
        await upload(url, content)

        file_name = _field(upload_info, "name") or "file"
        file_type = _field(upload_info, "file_type", "fileType")
        unique_id = _field(upload_info, "unique_id", "uniqueId")
        content_url = _field(upload_info, "content_url", "contentUrl")
        file_info = Attachment(
            content_type=CONTENT_TYPE_FILE_INFO,
            name=file_name,
            content_url=content_url,
            content={
                "uniqueId": unique_id,
                "fileType": file_type,
            },
        )
        await context.send_activity(
            Activity(
                type="message",
                text=f"<b>{file_name}</b> has been successfully uploaded.",
                attachments=[file_info],
            )
        )

    async def handle_file_consent(context: TurnContext, _state: Any) -> None:
        value = _mapping(context.activity.value)
        action = value.get("action")
        consent_context = _mapping(value.get("context"))
        file_id = _field(consent_context, "file_id", "fileId")
        filename = _field(consent_context, "filename") or "file"
        if not isinstance(file_id, str):
            logger.warning("File-consent activity did not include its file identifier")
            return

        if action == "decline":
            pending_uploads.pop(file_id, None)
            await context.send_activity(
                f"Declined. We won't upload file <b>{filename}</b>."
            )
            return

        if action != "accept":
            logger.warning("Ignoring unsupported file-consent action: %s", action)
            return

        content = pending_uploads.pop(file_id, None)
        if content is None:
            logger.warning("Ignoring file-consent action for unavailable file %s", file_id)
            return
        await context.send_activity(
            f"Accepted. Uploading <b>{filename}</b>..."
        )
        upload_info = _mapping(_field(value, "upload_info", "uploadInfo"))
        continuation_data = context.activity.model_dump(
            mode="json", by_alias=True, exclude_none=True
        )
        continuation_data["type"] = "message"
        for field in ("id", "name", "value", "replyToId"):
            continuation_data.pop(field, None)
        continuation_activity = Activity.model_validate(continuation_data)
        agent_id = (
            context.activity.recipient.id if context.activity.recipient else None
        )
        if not agent_id:
            logger.warning("Cannot continue file upload without the agent identifier")
            return

        async def continue_upload(continued_context: TurnContext) -> None:
            try:
                await upload_in_continuation(continued_context, upload_info, content)
            except Exception:
                logger.exception("File upload failed for %s", filename)

        async def run_upload() -> None:
            try:
                await adapter.continue_conversation(
                    agent_id, continuation_activity, continue_upload
                )
            except Exception:
                logger.exception("Could not continue file upload for %s", filename)

        task = asyncio.create_task(run_upload())
        upload_tasks.add(task)
        task.add_done_callback(upload_tasks.discard)

    @teams.file_consent.accept
    async def on_file_consent_accept(
        context: TurnContext, state: Any, _file_consent: Any
    ) -> None:
        await handle_file_consent(context, state)

    @teams.file_consent.decline
    async def on_file_consent_decline(
        context: TurnContext, state: Any, _file_consent: Any
    ) -> None:
        await handle_file_consent(context, state)

    @app.activity("message")
    async def on_message(context: TurnContext, _state: Any) -> None:
        content = await get_attachment_content(context)
        if content is None:
            await context.send_activity(
                "Welcome to the Agent Attachments sample! Please attach a file or "
                "image to save to your OneDrive."
            )
            return

        attachment = next(iter(context.activity.attachments or []), None)
        filename = (
            (attachment.name if attachment else None) or f"image_{uuid4()}.png"
        )
        file_id = str(uuid4())
        pending_uploads[file_id] = content
        await context.send_activity(
            f"Received <b>{filename}</b>. Requesting permission to save to your "
            "OneDrive..."
        )
        await send_file_consent(context, filename, file_id)

    async def wait_for_uploads() -> None:
        while upload_tasks:
            await asyncio.gather(*tuple(upload_tasks), return_exceptions=True)

    app.wait_for_uploads = wait_for_uploads
    return app

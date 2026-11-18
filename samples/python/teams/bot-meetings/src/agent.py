# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Agents SDK Teams meeting event handlers."""

import asyncio
import logging
import os
from collections.abc import Mapping
from typing import Any

from microsoft_agents.activity import load_configuration_from_env
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import AgentApplication, MemoryStorage
from microsoft_agents.hosting.msteams import TeamsTurnContext

from .cards import meeting_card
from .transcripts import (
    default_resolve_meeting,
    default_transcript,
    parse_vtt,
)

logger = logging.getLogger(__name__)


def _read_field(value: Any, name: str) -> Any:
    if isinstance(value, Mapping):
        if name in value:
            return value[name]
        camel_name = name.split("_")[0] + "".join(
            part.title() for part in name.split("_")[1:]
        )
        return value.get(camel_name, value.get(camel_name[:1].upper() + camel_name[1:]))
    if value is None:
        return None
    attribute = getattr(value, name, None)
    if attribute is not None:
        return attribute
    camel_name = name.split("_")[0] + "".join(
        part.title() for part in name.split("_")[1:]
    )
    return getattr(
        value,
        camel_name,
        getattr(value, camel_name[:1].upper() + camel_name[1:], None),
    )


def _meeting_details(meeting: Any) -> Any:
    return _read_field(meeting, "details")


def _card_text(text: str, **properties: Any) -> dict:
    return {"type": "TextBlock", "text": text, "wrap": True, **properties}


async def _get_meeting_info(
    context: Any, meeting_id: str, app: AgentApplication
) -> Any:
    teams_context = TeamsTurnContext(context, app)
    return await teams_context.api_client.meetings.get_by_id(meeting_id)


def create_agent(dependencies: Mapping[str, Any] | None = None) -> AgentApplication:
    """Create an isolated Teams agent with optional meeting service overrides."""
    supplied = dependencies or {}
    configuration = load_configuration_from_env(os.environ)
    connection_manager = supplied.get("connection_manager")
    if connection_manager is None:
        connection_manager = MsalConnectionManager(**configuration)
    adapter = supplied.get("adapter")
    if adapter is None:
        adapter = CloudAdapter(connection_manager=connection_manager)

    app = AgentApplication(
        storage=MemoryStorage(),
        adapter=adapter,
        connection_manager=connection_manager,
        **configuration,
    )

    async def default_meeting_info(context: Any, meeting_id: str) -> Any:
        return await _get_meeting_info(context, meeting_id, app)

    meeting_info = supplied.get("meeting_info") or default_meeting_info
    transcript = supplied.get("transcript") or default_transcript
    resolve_meeting = supplied.get("resolve_meeting") or default_resolve_meeting
    wait = supplied.get("wait") or asyncio.sleep

    async def on_meeting_start(context: Any, _state: Any) -> None:
        event = context.activity.value
        title = _read_field(event, "title") or ""
        start_time = _read_field(event, "start_time") or ""
        join_url = _read_field(event, "join_url") or ""
        await context.send_activity(
            meeting_card(
                [
                    _card_text(
                        "The meeting has started.",
                        weight="Bolder",
                        size="Large",
                    ),
                    _card_text(f"**Title:** {title}"),
                    _card_text(f"**Start Time:** {start_time}"),
                ],
                [
                    {
                        "type": "Action.OpenUrl",
                        "title": "Join Meeting",
                        "url": join_url,
                    }
                ],
            )
        )

    async def on_meeting_end(context: Any, _state: Any) -> None:
        channel_meeting = _read_field(
            _read_field(context.activity, "channel_data"), "meeting"
        )
        meeting_id = _read_field(channel_meeting, "id")
        meeting_data = None
        if meeting_id:
            try:
                meeting_data = await meeting_info(context, meeting_id)
            except Exception:
                logger.exception("Could not retrieve metadata for meeting %s", meeting_id)

        channel_details = _meeting_details(channel_meeting)
        info_details = _meeting_details(meeting_data)
        resource_id = (
            _read_field(channel_details, "ms_graph_resource_id")
            or _read_field(info_details, "ms_graph_resource_id")
            or ""
        )
        organizer = _read_field(meeting_data, "organizer")
        user_id = _read_field(organizer, "aad_object_id") or ""
        event = context.activity.value
        join_url = (
            _read_field(info_details, "join_url")
            or _read_field(channel_details, "join_url")
            or _read_field(event, "join_url")
            or ""
        )

        transcript_text = ""
        if user_id and (resource_id or (meeting_id and join_url)):
            await wait(30)
            for attempt in range(3):
                if not resource_id:
                    try:
                        resource_id = (
                            await resolve_meeting(context, join_url, user_id) or ""
                        )
                    except Exception:
                        logger.exception(
                            "Graph meeting lookup failed for organizer %s", user_id
                        )
                if resource_id:
                    try:
                        transcript_text = await transcript(
                            context, resource_id, user_id
                        )
                    except Exception:
                        logger.exception(
                            "Graph transcript retrieval failed for meeting resource %s",
                            resource_id,
                        )
                    if transcript_text:
                        break
                if attempt < 2:
                    await wait(10)

        parsed_transcript = parse_vtt(transcript_text) if transcript_text else ""
        transcript_lines = (
            [_card_text(line) for line in parsed_transcript.splitlines() if line]
            if parsed_transcript
            else [_card_text("Transcript not available for this meeting.")]
        )
        end_time = _read_field(event, "end_time") or ""
        await context.send_activity(
            meeting_card(
                [
                    _card_text(
                        "The meeting has ended.",
                        weight="Bolder",
                        size="Large",
                    ),
                    _card_text(f"**End Time:** {end_time}"),
                    _card_text("**Transcript:**", weight="Bolder"),
                    *transcript_lines,
                ]
            )
        )

    async def on_meeting_participant_join(context: Any, _state: Any) -> None:
        participant_event = context.activity.value
        members = _read_field(participant_event, "members") or []
        participant = members[0] if members else None
        user = _read_field(participant, "user")
        aad_object_id = _read_field(user, "aad_object_id")
        if not aad_object_id:
            return
        name = _read_field(user, "name") or "A participant"
        meeting = _read_field(participant, "meeting")
        role = _read_field(meeting, "role") or "a participant"
        await context.send_activity(
            meeting_card(
                [
                    _card_text(
                        f"{name} has joined the meeting as {role}.",
                        weight="Bolder",
                    )
                ]
            )
        )

    async def on_meeting_participant_leave(context: Any, _state: Any) -> None:
        participant_event = context.activity.value
        members = _read_field(participant_event, "members") or []
        participant = members[0] if members else None
        user = _read_field(participant, "user")
        if not _read_field(user, "aad_object_id"):
            return
        name = _read_field(user, "name") or "A participant"
        await context.send_activity(
            meeting_card(
                [
                    _card_text(
                        f"{name} has left the meeting.",
                        weight="Bolder",
                    )
                ]
            )
        )

    handlers = {
        "application/vnd.microsoft.meetingStart": on_meeting_start,
        "application/vnd.microsoft.meetingEnd": on_meeting_end,
        "application/vnd.microsoft.meetingParticipantJoin": on_meeting_participant_join,
        "application/vnd.microsoft.meetingParticipantLeave": on_meeting_participant_leave,
    }

    @app.activity("event")
    async def on_meeting_event(context: Any, state: Any) -> None:
        logger.info("Meeting event received: %s", context.activity.name)
        handler = handlers.get(context.activity.name or "")
        if handler is not None:
            await handler(context, state)
            logger.info("Meeting event handled: %s", context.activity.name)
        else:
            logger.warning("No handler for meeting event: %s", context.activity.name)

    return app

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Agents SDK Teams meeting event handlers."""

import asyncio
import logging
from collections.abc import Awaitable, Callable, Mapping
from typing import Any

from microsoft_agents.hosting.core import AgentApplication, MemoryStorage, TurnContext, TurnState
from microsoft_agents.hosting.core.authorization import Connections
from microsoft_agents.hosting.msteams import TeamsTurnContext

from .cards import meeting_card
from .transcripts import (
    default_resolve_meeting,
    default_transcript,
    parse_vtt,
)

logger = logging.getLogger(__name__)


def _teams_field(value: Any, name: str) -> Any:
    """Read an event payload field from Teams JSON or a Teams API model."""
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


def _card_text(text: str, **properties: Any) -> dict:
    return {"type": "TextBlock", "text": text, "wrap": True, **properties}


async def _get_meeting_info(
    context: TurnContext, meeting_id: str, app: AgentApplication
) -> Any:
    teams_context = TeamsTurnContext(context, app)
    return await teams_context.api_client.meetings.get_by_id(meeting_id)


def create_agent(
    configuration: dict,
    connection_manager: Connections,
    *,
    meeting_info: Callable[[TurnContext, str], Awaitable[Any]] | None = None,
    transcript: Callable[[TurnContext, str, str], Awaitable[str | None]] = default_transcript,
    resolve_meeting: Callable[[TurnContext, str, str], Awaitable[str | None]] = default_resolve_meeting,
    wait: Callable[[float], Awaitable[None]] = asyncio.sleep,
) -> AgentApplication:
    app = AgentApplication(
        storage=MemoryStorage(),
        connection_manager=connection_manager,
        **configuration,
    )

    async def default_meeting_info(context: TurnContext, meeting_id: str) -> Any:
        return await _get_meeting_info(context, meeting_id, app)

    fetch_meeting_info = meeting_info or default_meeting_info

    async def on_meeting_start(context: TurnContext, _state: TurnState) -> None:
        event = context.activity.value
        title = _teams_field(event, "title") or ""
        start_time = _teams_field(event, "start_time") or ""
        join_url = _teams_field(event, "join_url") or ""
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

    async def wait_for_transcript(
        context: TurnContext, resource_id: str, user_id: str, join_url: str
    ) -> str:
        # Graph can take time to publish a transcript after the meeting ends.
        await wait(30)
        for attempt in range(3):
            if attempt:
                await wait(10)

            if not resource_id:
                try:
                    resource_id = await resolve_meeting(context, join_url, user_id) or ""
                except Exception:
                    logger.exception("Graph meeting lookup failed for organizer %s", user_id)
                if not resource_id:
                    continue

            try:
                transcript_text = await transcript(context, resource_id, user_id)
            except Exception:
                logger.exception(
                    "Graph transcript retrieval failed for meeting resource %s", resource_id
                )
                continue
            if transcript_text:
                return transcript_text

        return ""

    async def on_meeting_end(context: TurnContext, _state: TurnState) -> None:
        channel_meeting = _teams_field(context.activity.channel_data, "meeting")
        meeting_id = _teams_field(channel_meeting, "id")
        meeting_data = None
        if meeting_id:
            try:
                meeting_data = await fetch_meeting_info(context, meeting_id)
            except Exception:
                logger.exception("Could not retrieve metadata for meeting %s", meeting_id)

        channel_details = _teams_field(channel_meeting, "details")
        info_details = _teams_field(meeting_data, "details")
        resource_id = (
            _teams_field(channel_details, "ms_graph_resource_id")
            or _teams_field(info_details, "ms_graph_resource_id")
            or ""
        )
        organizer = _teams_field(meeting_data, "organizer")
        user_id = _teams_field(organizer, "aad_object_id") or ""
        event = context.activity.value
        join_url = (
            _teams_field(info_details, "join_url")
            or _teams_field(channel_details, "join_url")
            or _teams_field(event, "join_url")
            or ""
        )

        transcript_text = ""
        if user_id and (resource_id or (meeting_id and join_url)):
            transcript_text = await wait_for_transcript(context, resource_id, user_id, join_url)

        parsed_transcript = parse_vtt(transcript_text) if transcript_text else ""
        transcript_lines = (
            [_card_text(line) for line in parsed_transcript.splitlines() if line]
            if parsed_transcript
            else [_card_text("Transcript not available for this meeting.")]
        )
        end_time = _teams_field(event, "end_time") or ""
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

    async def on_meeting_participant_join(context: TurnContext, _state: TurnState) -> None:
        participant_event = context.activity.value
        members = _teams_field(participant_event, "members") or []
        participant = members[0] if members else None
        user = _teams_field(participant, "user")
        aad_object_id = _teams_field(user, "aad_object_id")
        if not aad_object_id:
            return
        name = _teams_field(user, "name") or "A participant"
        meeting = _teams_field(participant, "meeting")
        role = _teams_field(meeting, "role") or "a participant"
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

    async def on_meeting_participant_leave(context: TurnContext, _state: TurnState) -> None:
        participant_event = context.activity.value
        members = _teams_field(participant_event, "members") or []
        participant = members[0] if members else None
        user = _teams_field(participant, "user")
        if not _teams_field(user, "aad_object_id"):
            return
        name = _teams_field(user, "name") or "A participant"
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
    async def on_meeting_event(context: TurnContext, state: TurnState) -> None:
        logger.info("Meeting event received: %s", context.activity.name)
        handler = handlers.get(context.activity.name or "")
        if handler is not None:
            await handler(context, state)
            logger.info("Meeting event handled: %s", context.activity.name)
        else:
            logger.warning("No handler for meeting event: %s", context.activity.name)

    return app

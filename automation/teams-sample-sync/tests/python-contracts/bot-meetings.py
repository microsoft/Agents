# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Meeting events preserve typed metadata, participants and transcript lifecycle."""

import json
import unittest
from unittest.mock import AsyncMock
from unittest.mock import Mock
from datetime import datetime, timezone
from types import SimpleNamespace

from harness import dispatch, sample_application


def event(name, value, channel_data=None):
    return {
        "type": "event", "name": f"application/vnd.microsoft.meeting{name}",
        "value": value, "channelData": channel_data or {},
    }


class MeetingContracts(unittest.IsolatedAsyncioTestCase):
    async def test_graph_transport_selects_newest_transcript_and_requests_vtt(self):
        from msgraph import GraphServiceClient
        from src.transcripts import get_meeting_transcript, parse_vtt

        adapter = Mock()
        adapter.base_url = "https://graph.microsoft.com/beta"
        adapter.send_async = AsyncMock(return_value=SimpleNamespace(value=[
            SimpleNamespace(id="older", created_date_time=datetime(2026, 10, 28, 9, tzinfo=timezone.utc)),
            SimpleNamespace(id="newer", created_date_time=datetime(2026, 10, 28, 10, tzinfo=timezone.utc)),
        ]))
        adapter.send_primitive_async = AsyncMock(return_value=b"WEBVTT\n<v Ada>Latest words</v>")
        client = GraphServiceClient(request_adapter=adapter)
        transcript = await get_meeting_transcript(client, "meeting-resource", "organizer")
        self.assertEqual(parse_vtt(transcript), "Ada: Latest words")
        metadata_request = adapter.send_async.await_args.args[0]
        content_request = adapter.send_primitive_async.await_args.args[0]
        self.assertEqual(metadata_request.url,
                         "https://graph.microsoft.com/beta/users/organizer/onlineMeetings/meeting-resource/transcripts")
        self.assertEqual(content_request.url,
                         "https://graph.microsoft.com/beta/users/organizer/onlineMeetings/meeting-resource/transcripts/newer/content")
        self.assertIn("text/vtt", content_request.headers.get("Accept"))

    async def test_start_preserves_title_time_and_join_action(self):
        app = sample_application()
        sent = await dispatch(app, event("Start", {
            "Title": "Design review", "StartTime": "2026-10-28T10:00:00Z",
            "JoinUrl": "https://teams.example.com/join",
        }))
        card = sent[0]["attachments"][0]["content"]
        self.assertEqual(card["body"][1]["text"], "**Title:** Design review")
        self.assertRegex(card["body"][2]["text"], r"10:00:00(?:Z|\+00:00)")
        self.assertEqual(card["actions"][0]["url"], "https://teams.example.com/join")

    async def test_end_preserves_resource_fallback_retries_and_vtt(self):
        async def meeting_info(context, identifier):
            self.assertEqual(identifier, "meeting-1")
            return {
                "organizer": {"aadObjectId": "organizer-1"},
                "details": {"msGraphResourceId": "graph-meeting-1"},
            }

        async def transcript(context, resource, user):
            self.assertEqual((resource, user), ("graph-meeting-1", "organizer-1"))
            return next(results)

        results = iter(["", "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\n<v Alice>Hello team</v>\n"])
        wait = AsyncMock()
        app = sample_application({"meeting_info": meeting_info, "transcript": transcript, "wait": wait})
        sent = await dispatch(app, event("End", {"EndTime": "2026-10-28T11:00:00Z"}, {
            "meeting": {"id": "meeting-1"},
        }))
        self.assertEqual([call.args[0] for call in wait.await_args_list], [30, 10])
        card = sent[0]["attachments"][0]["content"]
        self.assertIn("11:00:00", card["body"][1]["text"])
        self.assertEqual(card["body"][3]["text"], "Alice: Hello team")

    async def test_exhausted_retries_return_clear_fallback(self):
        transcript = AsyncMock(return_value="")
        wait = AsyncMock()
        app = sample_application({
            "meeting_info": AsyncMock(return_value={"organizer": {"aadObjectId": "user-1"}}),
            "transcript": transcript, "wait": wait,
        })
        sent = await dispatch(app, event("End", {}, {
            "meeting": {"id": "meeting-1", "details": {"msGraphResourceId": "graph-id"}},
        }))
        self.assertEqual(transcript.await_count, 3)
        self.assertEqual([call.args[0] for call in wait.await_args_list], [30, 10, 10])
        self.assertIn("Transcript not available", json.dumps(sent))

    async def test_channel_resource_takes_precedence_over_meeting_info(self):
        transcript = AsyncMock(return_value="WEBVTT\n<v Ada>Channel resource</v>")
        app = sample_application({
            "meeting_info": AsyncMock(return_value={
                "organizer": {"aadObjectId": "organizer-1"},
                "details": {"msGraphResourceId": "fallback-resource"},
            }),
            "transcript": transcript,
            "wait": AsyncMock(),
        })
        sent = await dispatch(app, event("End", {}, {
            "meeting": {"id": "meeting-1", "details": {"msGraphResourceId": "channel-resource"}},
        }))

        transcript.assert_awaited_once()
        self.assertEqual(transcript.await_args.args[1:], ("channel-resource", "organizer-1"))
        self.assertIn("Ada: Channel resource", json.dumps(sent))

    async def test_missing_graph_resource_returns_fallback_without_requesting_transcript(self):
        transcript = AsyncMock()
        app = sample_application({
            "meeting_info": AsyncMock(return_value={"organizer": {"aadObjectId": "organizer-1"}}),
            "transcript": transcript,
            "wait": AsyncMock(),
        })
        sent = await dispatch(app, event("End", {}, {"meeting": {"id": "meeting-1"}}))

        transcript.assert_not_awaited()
        self.assertIn("Transcript not available", json.dumps(sent))

    async def test_missing_meeting_metadata_does_not_call_graph(self):
        transcript = AsyncMock()
        app = sample_application({"transcript": transcript, "wait": AsyncMock()})
        sent = await dispatch(app, event("End", {}))
        transcript.assert_not_awaited()
        self.assertIn("Transcript not available", json.dumps(sent))

    async def test_participant_roles_and_agent_only_filtering(self):
        app = sample_application()
        participant = {"members": [{
            "user": {"aadObjectId": "aad-1", "name": "Ada"},
            "meeting": {"role": "Presenter"},
        }]}
        joined = await dispatch(app, event("ParticipantJoin", participant))
        self.assertEqual(joined[0]["attachments"][0]["content"]["body"][0]["text"],
                         "Ada has joined the meeting as Presenter.")
        left = await dispatch(app, event("ParticipantLeave", participant))
        self.assertEqual(left[0]["attachments"][0]["content"]["body"][0]["text"], "Ada has left the meeting.")
        for operation in ("ParticipantJoin", "ParticipantLeave"):
            with self.subTest(operation=operation):
                ignored = await dispatch(app, event(operation, {
                    "members": [{"user": {"name": "Agent"}, "meeting": {}}],
                }))
                self.assertEqual(ignored, [])

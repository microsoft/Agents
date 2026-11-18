# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Targeted reminders retain ownership, cancellation and proactive delivery."""

import unittest
from unittest.mock import AsyncMock

from harness import APPLICATION_ADAPTERS, dispatch, invoke_response, sample_application


GROUP = {"id": "chat-1;messageid=123", "isGroup": True, "conversationType": "groupChat"}


class TimerDriver:
    def __init__(self):
        self.timers = []

    def schedule(self, callback, seconds):
        timer = {"run": callback, "seconds": seconds, "cancelled": False}
        self.timers.append(timer)

        def cancel():
            timer["cancelled"] = True

        return cancel


def reminder_id(sent):
    return sent[0]["attachments"][0]["content"]["actions"][0]["data"]["reminderId"]


class TargetedContracts(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.driver = TimerDriver()
        self.reaction = AsyncMock()
        self.app = sample_application({
            "now": lambda: 1000, "schedule": self.driver.schedule, "reaction": self.reaction,
        })

    async def asyncTearDown(self):
        await self.app.close_background_tasks()

    def assert_private(self, activity, recipient):
        self.assertEqual(activity["recipient"]["id"], recipient)
        self.assertTrue(any(
            entity.get("type") == "activityTreatment" and entity.get("treatment") == "targeted"
            for entity in activity.get("entities", [])
        ))

    async def test_personal_reminder_confirms_and_delivers_without_targeting(self):
        sent = await dispatch(self.app, {
            "text": "remind me in 2 mins to review PR",
            "conversation": {"id": "personal-chat", "isGroup": False, "conversationType": "personal"},
        })
        self.assertFalse(any(e.get("treatment") == "targeted" for e in sent[0].get("entities", [])))
        await self.driver.timers[0]["run"]()
        adapter = APPLICATION_ADAPTERS[self.app]
        self.assertFalse(any(e.get("treatment") == "targeted" for e in adapter.sent[-1].get("entities", [])))

    async def test_mentioned_reminder_confirms_privately_and_delivers_to_recipient(self):
        sent = await dispatch(self.app, {
            "conversation": GROUP,
            "text": "<at>Agent</at> remind <at>Bob</at> in 2 mins to review PR",
            "entities": [
                {"type": "mention", "text": "<at>Agent</at>", "mentioned": {"id": "agent-1"}},
                {"type": "mention", "text": "<at>Bob</at>", "mentioned": {"id": "user-2", "name": "Bob"}},
            ],
        })
        self.assert_private(sent[0], "user-1")
        self.assertEqual(self.driver.timers[0]["seconds"], 120)
        await self.driver.timers[0]["run"]()
        adapter = APPLICATION_ADAPTERS[self.app]
        self.assertEqual(len(adapter.continued), 1)
        self.assertEqual(adapter.continued[0]["activity"]["conversation"]["id"], "chat-1")
        self.assert_private(adapter.sent[-1], "user-2")

    async def test_listing_and_cancel_enforce_ownership(self):
        created = await dispatch(self.app, {"text": "remind me in 1 hour meeting", "conversation": GROUP})
        identifier = reminder_id(created)
        outsider = {"id": "user-3", "name": "Charlie"}
        hidden = await dispatch(self.app, {"text": "my-reminders", "from": outsider, "conversation": GROUP})
        self.assertEqual(hidden[0]["text"], "You have no active reminders.")
        denied = await dispatch(self.app, {
            "text": f"cancel-reminder {identifier}", "from": outsider, "conversation": GROUP,
        })
        self.assertIn("only cancel reminders", denied[0]["text"])
        self.assertFalse(self.driver.timers[0]["cancelled"])
        await dispatch(self.app, {"text": f"cancel-reminder {identifier}", "conversation": GROUP})
        self.assertTrue(self.driver.timers[0]["cancelled"])

    async def test_suggested_actions_acknowledge_and_share_command_routing(self):
        for command in ("help", "remind me in 1s test", "my-reminders", "custom-command", None):
            sent = await dispatch(self.app, {
                "type": "invoke", "name": "suggestedActions/submit",
                "conversation": GROUP, "value": {"command": command},
            })
            self.assertEqual(invoke_response(sent)["status"], 200)
            if command == "custom-command":
                self.assertIn("Executing: custom-command", [item.get("text") for item in sent])
        self.assertEqual(self.driver.timers[0]["seconds"], 1)

    async def test_reaction_id_precedence(self):
        await dispatch(self.app, {"text": '<quoted messageId="quoted-1"/> add-reaction like', "replyToId": "reply-1"})
        await dispatch(self.app, {"text": "remove-reaction heart", "replyToId": "reply-1"})
        await dispatch(self.app, {"text": "add-reaction launch"})
        observed = [call.args[1:] for call in self.reaction.await_args_list]
        self.assertEqual(observed, [
            ("add", "quoted-1", "like"), ("delete", "reply-1", "heart"),
            ("add", "incoming-activity", "launch"),
        ])

    async def test_default_reaction_transport_uses_the_teams_client(self):
        from microsoft_teams.api import ApiClient

        app = sample_application({"schedule": TimerDriver().schedule})
        client = ApiClient("https://example.invalid")
        client.reactions.add = AsyncMock()
        client.reactions.delete = AsyncMock()
        try:
            await dispatch(app, {"text": "add-reaction like", "conversation": GROUP}, teams_client=client)
            client.reactions.add.assert_awaited_once_with(GROUP["id"], "incoming-activity", "like")
            await dispatch(app, {
                "text": "remove-reaction heart", "conversation": GROUP, "replyToId": "reply-1",
            }, teams_client=client)
            client.reactions.delete.assert_awaited_once_with(GROUP["id"], "reply-1", "heart")
        finally:
            await app.close_background_tasks()

    async def test_slash_reactions_are_blocked_and_prompt_preview_is_preserved(self):
        sent = await dispatch(self.app, {
            "text": "add-reaction like", "conversation": GROUP,
            "recipient": {"id": "agent-1", "isTargeted": True},
        })
        self.reaction.assert_not_awaited()
        self.assertIn("cannot be added via slash", sent[0]["text"])
        self.assert_private(sent[0], "user-1")
        self.assertTrue(any(
            entity.get("type") == "targetedMessageInfo"
            and entity.get("targetedMessageId") == "incoming-activity"
            for entity in sent[0].get("entities", [])
        ))

    async def test_help_and_fallback_keep_suggestions_and_ai_metadata(self):
        help_messages = await dispatch(self.app, {"text": "reminder-help", "conversation": GROUP})
        self.assert_private(help_messages[0], "user-1")
        self.assertRegex(help_messages[0]["text"], r"(?s)remind @John.*my-reminders.*add-reaction")
        self.assertTrue(help_messages[0]["suggestedActions"]["actions"])
        fallback = await dispatch(self.app, {"text": "hello", "conversation": GROUP})
        self.assertTrue(fallback[0]["suggestedActions"]["actions"])
        self.assertTrue(any(
            "AIGeneratedContent" in entity.get("additionalType", [])
            for entity in fallback[0].get("entities", [])
        ))

    async def test_card_cancel_dismiss_and_snooze_acknowledge(self):
        created = await dispatch(self.app, {"text": "remind me in 1h test", "conversation": GROUP})
        identifier = reminder_id(created)
        for verb, data in (
            ("cancel_reminder", {"reminderId": identifier}),
            ("snooze_reminder", {"reminderText": "test", "snoozeMinutes": 5}),
        ):
            sent = await dispatch(self.app, {
                "type": "invoke", "name": "adaptiveCard/action", "conversation": GROUP,
                "value": {"action": {
                    "type": "Action.Execute", "verb": verb, "data": {"action": verb, **data},
                }},
            })
            self.assertEqual(invoke_response(sent)["status"], 200)
        self.assertTrue(self.driver.timers[0]["cancelled"])
        self.assertEqual(self.driver.timers[-1]["seconds"], 300)
        confirmation = next(item for item in sent if item.get("attachments"))
        snoozed_id = reminder_id([confirmation])
        dismissed = await dispatch(self.app, {
            "type": "invoke", "name": "adaptiveCard/action", "conversation": GROUP,
            "value": {"action": {
                "type": "Action.Execute", "verb": "dismiss_reminder",
                "data": {"action": "dismiss_reminder", "reminderId": snoozed_id},
            }},
        })
        self.assertEqual(invoke_response(dismissed)["status"], 200)
        self.assertTrue(self.driver.timers[-1]["cancelled"])

    async def test_reaction_events_preserve_added_and_removed_behavior(self):
        added = await dispatch(self.app, {
            "type": "messageReaction", "reactionsAdded": [{
                "type": "like", "user": {"displayName": "Ada"},
            }],
        })
        self.assertTrue(any("like" in item.get("text", "") for item in added))
        removed = await dispatch(self.app, {
            "type": "messageReaction", "reactionsRemoved": [{
                "type": "heart", "user": {"displayName": "Ada"},
            }],
        })
        # Python's pinned source acknowledges removals; retain its explicit behavior.
        self.assertTrue(any("removed" in item.get("text", "") and "heart" in item.get("text", "") for item in removed))

    async def test_invalid_times_and_shutdown_do_not_leave_timers(self):
        for text in ("remind me in -1s invalid", "remind me in 999999999999999999999h overflow"):
            await dispatch(self.app, {"text": text, "conversation": GROUP})
        self.assertEqual(self.driver.timers, [])
        await dispatch(self.app, {"text": "remind me in 1h cleanup", "conversation": GROUP})
        await self.app.close_background_tasks()
        self.assertTrue(all(timer["cancelled"] for timer in self.driver.timers))

    async def test_application_instances_do_not_share_reminders(self):
        await dispatch(self.app, {"text": "remind me in 1h private", "conversation": GROUP})
        other = sample_application({"schedule": TimerDriver().schedule})
        try:
            sent = await dispatch(other, {"text": "my-reminders", "conversation": GROUP})
            self.assertEqual(sent[0]["text"], "You have no active reminders.")
        finally:
            await other.close_background_tasks()

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Cards parity through actual Agents/Teams SDK routing."""

import unittest

from harness import dispatch, invoke_response, sample_application


class CardContracts(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.app = sample_application()

    async def test_nested_forms_and_command_precedence(self):
        sent = await dispatch(self.app, {"text": "CARD ACTIONS and toggle visibility"})
        attachment = sent[0]["attachments"][0]
        self.assertEqual(attachment["contentType"], "application/vnd.microsoft.card.adaptive")
        card = attachment["content"]
        self.assertEqual(card["body"][0]["text"], "Adaptive Card Actions")
        self.assertEqual([action["type"] for action in card["actions"]], [
            "Action.OpenUrl", "Action.ShowCard", "Action.ShowCard",
        ])
        form = card["actions"][1]["card"]
        self.assertEqual(form["body"][0]["id"], "name")
        self.assertEqual(form["actions"][0]["verb"], "submit_name")
        nested = card["actions"][2]["card"]["actions"][0]["card"]
        self.assertIn("Welcome To Your New Card", nested["body"][0]["text"])

    async def test_visibility_targets_initially_hidden_element(self):
        sent = await dispatch(self.app, {"text": "toggle visibility"})
        card = sent[0]["attachments"][0]["content"]
        self.assertEqual(card["body"][1]["id"], "helloWorld")
        self.assertIs(card["body"][1]["isVisible"], False)
        self.assertEqual(card["actions"][0]["targetElements"], ["helloWorld"])

    async def test_submission_and_missing_name_preserve_invoke_envelope(self):
        for data, expected in (({"name": "Ada"}, "Ada"), ({}, "")):
            with self.subTest(data=data):
                sent = await dispatch(self.app, {
                    "type": "invoke", "name": "adaptiveCard/action",
                    "value": {"action": {
                        "type": "Action.Execute", "verb": "submit_name", "data": data,
                    }},
                })
                self.assertIn(f"Data Submitted: {expected}", [activity.get("text") for activity in sent])
                response = invoke_response(sent)
                self.assertEqual(response["status"], 200)
                self.assertEqual(response["body"], {
                    "statusCode": 200,
                    "type": "application/vnd.microsoft.activity.message",
                    "value": "Action processed successfully",
                })

    async def test_unknown_message_advertises_both_commands(self):
        sent = await dispatch(self.app, {"text": "unknown command"})
        self.assertRegex(sent[0]["text"], r"card actions.*toggle visibility")
        self.assertNotIn("Cards Bot", sent[0]["text"])

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Dialog behavior and served assets through real SDK invokes and HTTP."""

import unittest
from urllib.request import urlopen

from harness import dispatch, invoke_response, sample_application, sample_http


class DialogContracts(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.app = sample_application({"endpoint": "https://agent.example.com"})

    async def task(self, operation, data):
        sent = await dispatch(self.app, {
            "type": "invoke", "name": f"task/{operation}", "value": {"data": data},
        })
        response = invoke_response(sent)
        self.assertEqual(response["status"], 200)
        return sent, response["body"]["task"]

    async def test_all_three_launch_actions(self):
        sent = await dispatch(self.app, {"text": "show tasks"})
        actions = sent[0]["attachments"][0]["content"]["actions"]
        self.assertEqual([action["title"] for action in actions], [
            "Adaptive Card", "Custom Form", "Multi-step Form",
        ])
        self.assertTrue(all(action["data"]["msteams"]["type"] == "task/fetch" for action in actions))

    async def test_fetch_selects_input_custom_url_and_name_step(self):
        _, input_task = await self.task("fetch", {"verb": "adaptive_card"})
        self.assertEqual(input_task["value"]["card"]["content"]["body"][1]["id"], "usertext")
        _, custom = await self.task("fetch", {"verb": "custom_form"})
        self.assertEqual(custom["value"]["url"], "https://agent.example.com/customform")
        self.assertEqual(custom["value"]["fallbackUrl"], custom["value"]["url"])
        _, steps = await self.task("fetch", {"verb": "multi_step_form"})
        self.assertEqual(steps["value"]["card"]["content"]["body"][1]["id"], "name")

    async def test_multistep_state_and_completion(self):
        _, first = await self.task("submit", {"verb": "multi_step_1", "name": "Ada"})
        card = first["value"]["card"]["content"]
        self.assertEqual(card["body"][1]["id"], "email")
        self.assertEqual(card["actions"][0]["data"]["name"], "Ada")
        sent, second = await self.task("submit", {
            "verb": "multi_step_2", "name": "Ada", "email": "ada@example.com",
        })
        self.assertIn("Hi Ada, thanks for submitting! Your email is ada@example.com", [item.get("text") for item in sent])
        self.assertEqual(second, {"type": "message", "value": "Multi-step form completed!"})

    async def test_text_and_custom_submission_envelopes(self):
        sent, text = await self.task("submit", {"verb": "adaptive_card_submit", "usertext": "Hello"})
        self.assertIn("You submitted: Hello", [item.get("text") for item in sent])
        self.assertEqual(text, {"type": "message", "value": "Thanks for submitting!"})
        sent, custom = await self.task("submit", {
            "verb": "custom_form_submit", "name": "Grace", "email": "grace@example.com",
        })
        self.assertIn(
            "Hi Grace, thanks for submitting! Your email is grace@example.com",
            [item.get("text") for item in sent],
        )
        self.assertEqual(custom, {"type": "message", "value": "Form submitted successfully"})

    async def test_custom_form_route_serves_teams_dialog_page(self):
        with sample_http() as url:
            with urlopen(url + "/customform", timeout=5) as response:
                self.assertEqual(response.status, 200)
                html = response.read().decode("utf-8")
        self.assertIn("microsoftTeams", html)
        self.assertRegex(html, r"submitTask|dialog\.url\.submit")

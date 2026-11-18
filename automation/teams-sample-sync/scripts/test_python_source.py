"""Offline checks for the trusted, non-executing syntax inventory."""

import importlib.util
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "python_source", Path(__file__).with_name("python_source.py")
)
SOURCE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SOURCE)


class SourceInventoryTests(unittest.TestCase):
    def test_extracted_sdk_registrations_fail_explicitly(self):
        for route in ("message_extensions.query('search')", "file_consent.accept()", "meetings.start()"):
            with self.subTest(route=route):
                facts = self.capabilities(
                    "from microsoft_agents.hosting.core import AgentApplication\n"
                    "from microsoft_agents.hosting.msteams import TeamsAgentExtension\n"
                    "def register_routes(extension):\n"
                    f"    extension.{route}\n"
                    "app = AgentApplication()\nteams = TeamsAgentExtension(app)\nregister_routes(teams)\n"
                )
                self.assertTrue(facts["bot"])
                self.assertIn("Unresolved SDK registration", facts["errors"][0])

    def test_module_alias_and_annotated_construction(self):
        facts = self.capabilities(
            "import microsoft_agents.hosting.core as core\n"
            "import microsoft_agents.hosting.msteams as mst\n"
            "app: core.AgentApplication = core.AgentApplication()\n"
            "teams = mst.TeamsAgentExtension(app)\n"
            "alias = teams\nalias.message_extensions.query('search')\n"
        )
        self.assertTrue(facts["bot"])
        self.assertEqual(facts["queryCommands"][0]["id"], "search")
        self.assertEqual(facts["errors"], [])

    def test_payload_getattr_is_allowed_but_dynamic_sdk_lookup_fails(self):
        facts = self.capabilities(
            "from microsoft_agents.hosting.core import AgentApplication\n"
            "from microsoft_agents.hosting.msteams import TeamsAgentExtension\n"
            "app = AgentApplication()\nteams = TeamsAgentExtension(app)\n"
            "value = getattr(payload, 'title', '')\n"
            "route = getattr(teams, route_name)\n"
        )
        self.assertEqual(len(facts["errors"]), 1)
        self.assertIn("dynamic registration", facts["errors"][0])

    def capabilities(self, source):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "agent.py").write_text(source, encoding="utf-8")
            return SOURCE.inspect_sources(root)[0]["capabilities"]

    def test_sdk_aliases_and_constant_command_ids(self):
        facts = self.capabilities(
            "from microsoft_agents.hosting.core import AgentApplication as App\n"
            "from microsoft_agents.hosting.msteams import TeamsAgentExtension as Teams\n"
            "app = App()\nteams = Teams(app)\nSEARCH = 'search'\n"
            "@teams.message_extensions.query(SEARCH)\n"
            "async def search(context, state, query):\n    pass\n"
        )
        self.assertTrue(facts["bot"])
        self.assertTrue(facts["composeExtensions"])
        self.assertEqual(facts["queryCommands"][0]["id"], "search")
        self.assertEqual(facts["errors"], [])

    def test_dynamic_selectors_fail_explicitly(self):
        facts = self.capabilities(
            "from microsoft_agents.hosting.core import AgentApplication\n"
            "from microsoft_agents.hosting.msteams import TeamsAgentExtension\n"
            "app = AgentApplication()\nteams = TeamsAgentExtension(app)\n"
            "teams.message_extensions.query(command_name())\n"
        )
        self.assertIn("dynamic command selector", facts["errors"][0])

    def test_parameter_shadowing_does_not_masquerade_as_sdk_construction(self):
        facts = self.capabilities(
            "from microsoft_agents.hosting.core import AgentApplication as App\n"
            "def build(App):\n    app = App()\n"
        )
        self.assertFalse(facts["bot"])

    def test_file_consent_and_participant_permissions(self):
        facts = self.capabilities(
            "from microsoft_agents.hosting.core import AgentApplication\n"
            "from microsoft_agents.hosting.msteams import TeamsAgentExtension\n"
            "app = AgentApplication()\nteams = TeamsAgentExtension(app)\n"
            "teams.file_consent.accept()\nteams.meetings.participants_join()\n"
        )
        self.assertTrue(facts["files"])
        self.assertIn("OnlineMeetingParticipant.Read.Chat", facts["resourceSpecificPermissions"])

    def test_inventory_preserves_aliases_and_async_decorators_without_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "agent.py").write_text(
                "from microsoft_agents.hosting.core import AgentApplication as App\n"
                "raise RuntimeError('must never execute')\n"
                "app = App()\n"
                "@app.message('hello')\n"
                "async def hello(context):\n    pass\n",
                encoding="utf-8",
            )
            inventory = SOURCE.inspect_sources(root)
            self.assertEqual(inventory[0]["imports"][0]["names"][0]["alias"], "App")
            self.assertEqual(inventory[0]["decorators"][0]["owner"], "hello")
            self.assertEqual(inventory[0]["decorators"][0]["expression"], "app.message('hello')")

    def test_invalid_syntax_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "agent.py").write_text("async def broken(", encoding="utf-8")
            with self.assertRaises(SyntaxError):
                SOURCE.inspect_sources(root)

    def test_empty_tree_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "no Python files"):
                SOURCE.inspect_sources(Path(directory))

    def test_environment_sources_are_excluded(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "agent.py").write_text("pass", encoding="utf-8")
            environment = root / ".venv"
            environment.mkdir()
            (environment / "malicious.py").write_text("invalid syntax !", encoding="utf-8")
            self.assertEqual(len(SOURCE.inspect_sources(root)), 1)


if __name__ == "__main__":
    unittest.main()

"""Dependency policy regressions run without installing candidate requirements."""

import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location(
    "python_requirements", Path(__file__).with_name("python_requirements.py")
)
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)


class RequirementPolicyTests(unittest.TestCase):
    def test_rejects_install_source_overrides(self):
        for declaration in (
            "--index-url https://evil.example/simple", "-r ../requirements.txt",
            "-e .", "package @ https://evil.example/package.whl",
            "package @ file:///tmp/package", "./package", "package>=1 --extra-index-url x",
        ):
            with self.subTest(declaration=declaration), self.assertRaises(ValueError):
                POLICY.parse_declarations(declaration)

    def test_normalized_duplicate_names_fail(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            POLICY.parse_declarations("Some_Package==1\nsome-package==2")

    def test_legitimate_teams_api_dependency_is_allowed(self):
        declarations = POLICY.parse_declarations("microsoft-teams-api==2.1.0")
        self.assertIn("microsoft-teams-api", declarations)

    def test_legacy_application_runtime_fails(self):
        with self.assertRaisesRegex(ValueError, "Legacy"):
            POLICY.parse_declarations("microsoft-teams-apps==2.1.0")

    def test_selected_sdk_requires_exact_unconditional_version(self):
        packages = {"microsoft-agents-activity": "1.9.0"}
        for declaration in (
            "microsoft-agents-activity>=1.9.0", "microsoft-agents-activity==1.9.*",
            'microsoft-agents-activity==1.9.0; python_version >= "3.11"',
            "microsoft-agents-activity[extra]==1.9.0",
        ):
            with self.subTest(declaration=declaration):
                self.assertTrue(POLICY.validate_sdk_declarations(declaration, packages))
        self.assertEqual(POLICY.validate_sdk_declarations("microsoft_agents_activity==1.9.0", packages), [])

    def test_missing_and_unselected_sdk_dependencies_fail(self):
        errors = POLICY.validate_sdk_declarations(
            "microsoft-agents-hosting-core==1.9.0", {"microsoft-agents-activity": "1.9.0"}
        )
        self.assertEqual(len(errors), 2)


if __name__ == "__main__":
    unittest.main()

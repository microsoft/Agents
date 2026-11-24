"""Deterministic SDK policy tests; no network or candidate imports."""

import importlib.util
from pathlib import Path
import unittest
import json
import subprocess
import sys


SPEC = importlib.util.spec_from_file_location(
    "python_sdk_policy", Path(__file__).with_name("python_sdk_policy.py")
)
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)


def feeds():
    return {
        name: [{
            "name": name,
            "version": "1.9.0",
            "requires_python": ">=3.11",
            "requires_dist": [],
            "yanked": False,
        }]
        for name in POLICY.SDK_PACKAGES
    }


class SdkPolicyTests(unittest.TestCase):
    def test_cli_rejects_actual_interpreter_runtime_mismatch(self):
        result = subprocess.run(
            [sys.executable, "-I", str(Path(__file__).with_name("python_sdk_policy.py"))],
            input=json.dumps({"operation": "runtime", "pythonVersion": "0.0.0"}),
            text=True, capture_output=True, timeout=10,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("runtime differs", result.stderr)

    def test_serialized_selection_rejects_tampered_package_map(self):
        selection = POLICY.select_sdk_versions("1.9.0", "3.11.9", feeds())
        POLICY.assert_selection(selection, "1.9.0", "3.11.9")
        selection["packages"][POLICY.SDK_PACKAGES[0]] = "1.9.1"
        with self.assertRaisesRegex(ValueError, "identity"):
            POLICY.assert_selection(selection, "1.9.0", "3.11.9")

    def test_target_markers_use_linux_even_on_a_windows_developer_machine(self):
        metadata = feeds()
        metadata[POLICY.SDK_PACKAGES[1]][0]["requires_dist"] = [
            'microsoft-agents-activity<1.9; sys_platform == "linux"'
        ]
        with self.assertRaisesRegex(ValueError, "No compatible"):
            POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)

    def test_malformed_metadata_fails_explicitly(self):
        metadata = feeds()
        metadata[POLICY.SDK_PACKAGES[0]][0]["yanked"] = "false"
        with self.assertRaisesRegex(ValueError, "boolean yanked"):
            POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)

    def test_runtime_requires_an_exact_patch_version(self):
        with self.assertRaisesRegex(ValueError, "exact stable patch"):
            POLICY.select_sdk_versions("1.9.0", "3.11", feeds())

    def test_extras_activate_transitive_sdk_constraints(self):
        metadata = feeds()
        metadata[POLICY.SDK_PACKAGES[1]][0]["requires_dist"] = [
            'microsoft-agents-activity[extended]>=1.9'
        ]
        metadata[POLICY.SDK_PACKAGES[0]][0]["requires_dist"] = [
            'microsoft-agents-hosting-core<1.9; extra == "extended"'
        ]
        with self.assertRaisesRegex(ValueError, "No compatible"):
            POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)

    def test_runtime_minimum_is_enforced(self):
        with self.assertRaisesRegex(ValueError, "No compatible"):
            POLICY.select_sdk_versions("1.9.0", "3.10.9", feeds())

    def test_yanked_and_prerelease_versions_are_excluded(self):
        metadata = feeds()
        name = POLICY.SDK_PACKAGES[0]
        metadata[name].extend([
            {**metadata[name][0], "version": "1.10.0", "yanked": True},
            {**metadata[name][0], "version": "1.11.0rc1"},
        ])
        selection = POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)
        self.assertEqual(selection["packages"][name], "1.9.0")

    def test_active_dependency_conflict_fails(self):
        metadata = feeds()
        metadata[POLICY.SDK_PACKAGES[1]][0]["requires_dist"] = [
            'Microsoft_Agents_Activity<1.9; python_version >= "3.11"'
        ]
        with self.assertRaisesRegex(ValueError, "No compatible"):
            POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)

    def test_inactive_marker_does_not_create_conflict(self):
        metadata = feeds()
        metadata[POLICY.SDK_PACKAGES[1]][0]["requires_dist"] = [
            'microsoft-agents-activity<1.9; python_version < "3.11"'
        ]
        self.assertTrue(POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata))

    def test_missing_package_fails(self):
        metadata = feeds()
        del metadata[POLICY.SDK_PACKAGES[0]]
        with self.assertRaisesRegex(ValueError, "exactly"):
            POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)

    def test_identity_changes_when_non_headline_package_changes(self):
        metadata = feeds()
        before = POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)
        metadata[POLICY.SDK_PACKAGES[0]][0]["version"] = "1.9.1"
        after = POLICY.select_sdk_versions("1.9.0", "3.11.9", metadata)
        self.assertEqual(before["selectedAgentsSdkVersion"], after["selectedAgentsSdkVersion"])
        self.assertNotEqual(before["releaseTag"], after["releaseTag"])


if __name__ == "__main__":
    unittest.main()

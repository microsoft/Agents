"""Preparation preserves Windows infrastructure variables without leaking credentials."""

import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("python_prepare", Path(__file__).with_name("python_prepare.py"))
PREPARE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PREPARE)


class PreparationTests(unittest.TestCase):
    def test_committed_lock_checks_non_sdk_declarations_and_preserves_hashes(self):
        lock = "packaging==26.2 --hash=sha256:" + "a" * 64 + "\n"
        self.assertIn("--hash=sha256:" + "a" * 64,
                      PREPARE.locked_resolution_input("packaging>=26", lock))
        with self.assertRaisesRegex(ValueError, "does not satisfy"):
            PREPARE.locked_resolution_input("packaging>=27", lock)

    def test_committed_lock_resolution_preserves_requested_extras(self):
        lock = "sample==1.0 --hash=sha256:" + "b" * 64 + "\n"
        self.assertIn("sample[feature]==1.0", PREPARE.locked_resolution_input("sample[feature]>=1", lock))

    def test_environment_filter_preserves_systemroot_case_insensitively(self):
        with patch.dict(os.environ, {"SYSTEMROOT": "Windows", "GITHUB_TOKEN": "secret"}, clear=True):
            with patch.object(PREPARE.subprocess, "run") as execute:
                PREPARE.run(["python", "--version"], ".")
        environment = execute.call_args.kwargs["env"]
        self.assertEqual(environment["SYSTEMROOT"], "Windows")
        self.assertNotIn("GITHUB_TOKEN", environment)
        self.assertTrue(execute.call_args.kwargs["check"])
        self.assertEqual(execute.call_args.kwargs["timeout"], 180)

    def test_existing_environment_is_never_reused(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "must not already exist"):
                PREPARE.prepare_environment("packaging==26.2", {}, directory)

    def test_untrusted_requirements_fail_before_any_process_runs(self):
        with patch.object(PREPARE.subprocess, "run") as execute:
            with self.assertRaises(ValueError):
                PREPARE.prepare_environment("--index-url https://evil.example", {}, "unused")
            execute.assert_not_called()


if __name__ == "__main__":
    unittest.main()

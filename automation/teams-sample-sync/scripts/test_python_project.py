"""Supported project layout checks do not import or install candidate code."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
import json
import subprocess
import sys


SPEC = importlib.util.spec_from_file_location("python_project", Path(__file__).with_name("python_project.py"))
PROJECT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROJECT)


class ProjectTests(unittest.TestCase):
    def scaffold(self, root):
        (root / "src").mkdir()
        (root / "src/main.py").write_text("raise RuntimeError('never execute')", encoding="utf-8")
        (root / "src/agent.py").write_text(
            "from microsoft_agents.hosting.core import AgentApplication\napp = AgentApplication()",
            encoding="utf-8",
        )
        (root / "requirements.txt").write_text("microsoft-agents-activity==1.9.0", encoding="utf-8")

    def test_inspection_does_not_execute_entry_point(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.scaffold(root)
            result = PROJECT.inspect_project(root, {"microsoft-agents-activity": "1.9.0"})
            self.assertEqual(result["errors"], [])

    def test_build_configuration_is_rejected_before_resolution(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.scaffold(root)
            (root / "setup.py").write_text("raise RuntimeError()", encoding="utf-8")
            self.assertIn("build or install configuration", PROJECT.inspect_project(root, {})["errors"][0])

    def test_candidate_defects_return_repairable_evidence(self):
        for defect in ("missing", "syntax", "requirements"):
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                self.scaffold(root)
                if defect == "missing":
                    (root / "src/agent.py").unlink()
                elif defect == "syntax":
                    (root / "src/agent.py").write_text("async def broken(", encoding="utf-8")
                else:
                    (root / "requirements.txt").write_text("--index-url https://invalid", encoding="utf-8")
                result = PROJECT.inspect_project(root, {})
                self.assertTrue(result["errors"])
                self.assertEqual(result["capabilities"]["errors"], result["errors"])
                process = subprocess.run(
                    [sys.executable, "-I", "-B", str(Path(PROJECT.__file__))],
                    input=json.dumps({"root": str(root), "packages": {}}),
                    capture_output=True, text=True, timeout=15,
                )
                self.assertEqual(process.returncode, 0, process.stderr)
                self.assertEqual(json.loads(process.stdout)["errors"], result["errors"])

    def test_missing_application_construction_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.scaffold(root)
            (root / "src/agent.py").write_text("pass", encoding="utf-8")
            self.assertIn("must construct", PROJECT.inspect_project(root, {})["errors"][-1])


if __name__ == "__main__":
    unittest.main()

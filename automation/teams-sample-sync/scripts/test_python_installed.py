"""Installed metadata checks reject stale, missing, extra and executable evidence."""

import importlib.util
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location("python_installed", Path(__file__).with_name("python_installed.py"))
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)
LOCK = "sample==1.0 --hash=sha256:" + "a" * 64 + "\n"


class InstalledPolicyTests(unittest.TestCase):
    def distribution(self, site, name="sample", version="1.0"):
        metadata = site / f"{name}-{version}.dist-info"
        metadata.mkdir()
        (metadata / "METADATA").write_text(
            f"Metadata-Version: 2.1\nName: {name}\nVersion: {version}\n", encoding="utf-8"
        )

    def test_matching_metadata_passes_without_imports(self):
        with tempfile.TemporaryDirectory() as directory:
            site = Path(directory)
            self.distribution(site)
            (site / "sample.py").write_text("raise RuntimeError('never import')", encoding="utf-8")
            self.assertEqual(POLICY.check_installed(site, LOCK, {"sample": "1.0"}), [])

    def test_missing_and_stale_versions_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            site = Path(directory)
            self.assertTrue(POLICY.check_installed(site, LOCK, {}))
            self.distribution(site, version="0.9")
            self.assertTrue(POLICY.check_installed(site, LOCK, {}))

    def test_extra_distribution_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            site = Path(directory)
            self.distribution(site)
            self.distribution(site, name="extra")
            self.assertIn("outside the lock", POLICY.check_installed(site, LOCK, {})[0])

    def test_pth_files_fail_before_loading_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            site = Path(directory)
            (site / "evil.pth").write_text("import os", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "executable path configuration"):
                POLICY.check_installed(site, LOCK, {})


if __name__ == "__main__":
    unittest.main()

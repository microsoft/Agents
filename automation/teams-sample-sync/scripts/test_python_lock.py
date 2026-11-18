"""Hash-lock generation rejects untrusted or mismatched resolver evidence."""

import copy
import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location("python_lock", Path(__file__).with_name("python_lock.py"))
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)


def report():
    return {"version": "1", "pip_version": "26.1.1", "install": [{
        "is_direct": False, "is_yanked": False,
        "metadata": {"name": "sample", "version": "1.0"},
        "download_info": {
            "url": "https://files.pythonhosted.org/packages/sample-1.0-py3-none-any.whl",
            "archive_info": {"hashes": {"sha256": "a" * 64}},
        },
    }]}


class LockPolicyTests(unittest.TestCase):
    def test_unpinned_resolver_report_is_rejected(self):
        data = report()
        data["pip_version"] = "25.0"
        with self.assertRaisesRegex(ValueError, "pinned pip"):
            POLICY.lock_from_report(data, {})

    def test_lock_preserves_exact_versions_and_hashes(self):
        lock = POLICY.lock_from_report(report(), {"sample": "1.0"})
        self.assertIn("sample==1.0 --hash=sha256:" + "a" * 64, lock)

    def test_selected_sdk_mismatch_fails(self):
        with self.assertRaisesRegex(ValueError, "frozen selection"):
            POLICY.lock_from_report(report(), {"sample": "2.0"})

    def test_alternate_hosts_and_source_archives_fail(self):
        for url in (
            "https://evil.example/sample-1.0-py3-none-any.whl",
            "https://files.pythonhosted.org/sample-1.0.tar.gz",
            "file:///tmp/sample-1.0-py3-none-any.whl",
        ):
            data = report()
            data["install"][0]["download_info"]["url"] = url
            with self.subTest(url=url), self.assertRaises(ValueError):
                POLICY.lock_from_report(data, {})

    def test_duplicate_distributions_fail(self):
        data = report()
        data["install"].append(copy.deepcopy(data["install"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            POLICY.lock_from_report(data, {})


if __name__ == "__main__":
    unittest.main()

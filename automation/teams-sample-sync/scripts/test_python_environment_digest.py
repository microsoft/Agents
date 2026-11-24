import importlib.util
import os
import shutil
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location("environment_digest", Path(__file__).with_name("python_environment_digest.py"))
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)


class EnvironmentDigestTests(unittest.TestCase):
    @unittest.skipUnless(os.name == "nt", "Windows extended-path regression")
    def test_long_installed_package_paths_are_fingerprinted(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            installed = Path("\\\\?\\" + str(root))
            package = installed / ("generated_api_" + "x" * 110) / ("request_builder_" + "y" * 110)
            package.mkdir(parents=True)
            source = package / "request.py"
            source.write_text("original", encoding="utf-8")

            try:
                original = POLICY.environment_digest(root)
                self.assertEqual(original, POLICY.environment_digest(installed))
                source.write_text("changed", encoding="utf-8")
                self.assertNotEqual(original, POLICY.environment_digest(root))
            finally:
                shutil.rmtree(package.parent)

    def test_modified_added_and_removed_files_change_fingerprint(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / "package.py"
            file.write_text("original", encoding="utf-8")
            original = POLICY.environment_digest(root)
            file.write_text("modified", encoding="utf-8")
            self.assertNotEqual(original, POLICY.environment_digest(root))
            file.write_text("original", encoding="utf-8")
            self.assertEqual(original, POLICY.environment_digest(root))
            extra = root / "extra.py"
            extra.write_text("injected", encoding="utf-8")
            self.assertNotEqual(original, POLICY.environment_digest(root))
            extra.unlink()
            file.unlink()
            self.assertNotEqual(original, POLICY.environment_digest(root))


if __name__ == "__main__":
    unittest.main()

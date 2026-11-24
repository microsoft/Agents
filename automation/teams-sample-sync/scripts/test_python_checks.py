"""Protected checks fail closed for absent, empty, and skipped suites."""

import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
import subprocess
import sys
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("python_checks", Path(__file__).with_name("python_checks.py"))
CHECKS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKS)


class ProtectedRunnerTests(unittest.TestCase):
    def write_http_host(self, root, anonymous_status=401):
        (root / "src").mkdir()
        (root / "src/main.py").write_text(
            "import json, os\nfrom pathlib import Path\n"
            "from http.server import HTTPServer, BaseHTTPRequestHandler\n"
            "class Handler(BaseHTTPRequestHandler):\n"
            "    def record(self):\n"
            "        with Path('requests.jsonl').open('a') as log:\n"
            "            log.write(json.dumps({'method': self.command, 'path': self.path,\n"
            "                'configured': bool(os.environ.get('CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID')),\n"
            "                'ambient': sorted(key for key in os.environ if key.endswith('__AMBIENT_SMOKE_TEST'))}) + '\\n')\n"
            "    def do_GET(self):\n        self.record()\n        self.send_response(200)\n        self.end_headers()\n"
            "    def do_POST(self):\n"
            "        self.rfile.read(int(self.headers.get('Content-Length', '0')))\n"
            f"        self.record()\n        self.send_response({anonymous_status})\n        self.end_headers()\n"
            "HTTPServer(('127.0.0.1', int(os.environ['PORT'])), Handler).serve_forever()\n",
            encoding="utf-8",
        )

    def run_check(self, root, mode, *extra):
        return subprocess.run(
            [sys.executable, "-I", "-B", str(Path(CHECKS.__file__)), mode, str(root), *extra],
            capture_output=True, text=True, timeout=15,
        )

    def test_external_import_request_is_rejected_even_when_caught(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "src/agent.py").write_text(
                "import socket\ntry:\n    socket.getaddrinfo('example.com', 443)\n"
                "except RuntimeError:\n    pass\ndef create_agent():\n    pass\n",
                encoding="utf-8",
            )
            result = self.run_check(root, "import")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Unexpected external requests", result.stderr)

    def test_external_contract_socket_request_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract = root / "contract.py"
            contract.write_text(
                "import unittest, socket\nclass Contract(unittest.TestCase):\n"
                "    def test_request(self):\n"
                "        with socket.socket() as client:\n"
                "            client.connect(('203.0.113.1', 443))\n",
                encoding="utf-8",
            )
            result = self.run_check(root, "contracts", str(contract))
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Unexpected external request", result.stderr)

    def test_spawned_http_host_cannot_hide_external_request(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "src/main.py").write_text(
                "import socket\ntry:\n    socket.getaddrinfo('example.com', 443)\n"
                "except RuntimeError:\n    pass\n",
                encoding="utf-8",
            )
            result = self.run_check(root, "smoke")
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Unexpected external request", result.stderr)

    def test_candidate_bytecode_is_rejected_before_import_or_contract_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "src/agent.pyc").write_bytes(b"untrusted cache")
            with self.assertRaisesRegex(ValueError, "untrusted bytecode"):
                CHECKS.import_application(root)
            with self.assertRaisesRegex(ValueError, "untrusted bytecode"):
                CHECKS.run_contracts(root, root / "missing_contract.py")

    def test_http_smoke_uses_configured_port_and_stops_server(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_http_host(root)
            with patch.dict(os.environ, {
                "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID": "ambient-identity-not-used",
                "CONNECTIONS__AMBIENT_SMOKE_TEST": "must-be-stripped",
                "CONNECTIONSMAP__AMBIENT_SMOKE_TEST": "must-be-stripped",
                "AGENTAPPLICATION__AMBIENT_SMOKE_TEST": "must-be-stripped",
            }):
                CHECKS.http_smoke(root)
            observed = [json.loads(line) for line in (root / "requests.jsonl").read_text().splitlines()]
            self.assertEqual(observed, [
                {"method": "GET", "path": "/", "configured": False, "ambient": []},
                {"method": "GET", "path": "/", "configured": True, "ambient": []},
                {"method": "POST", "path": "/api/messages", "configured": True, "ambient": []},
            ])

    def test_http_smoke_rejects_anonymous_success_and_server_errors(self):
        for status in (200, 500):
            with self.subTest(status=status), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                self.write_http_host(root, status)
                with self.assertRaisesRegex(ValueError, f"anonymous POST.*{status}"):
                    CHECKS.http_smoke(root)

    def test_http_smoke_detects_startup_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "src/main.py").write_text("raise SystemExit(1)", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "exited before readiness"):
                CHECKS.http_smoke(root)

    def test_missing_suite_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(ValueError, "missing"):
                CHECKS.run_contracts(root, root / "missing.py")

    def test_empty_suite_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract = root / "contract.py"
            contract.write_text("pass", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "zero tests"):
                CHECKS.run_contracts(root, contract)

    def test_skipped_suite_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contract = root / "contract.py"
            contract.write_text(
                "import unittest\nclass Contract(unittest.TestCase):\n"
                "    @unittest.skip('not allowed')\n    def test_behavior(self):\n        pass\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "execute and pass"):
                CHECKS.run_contracts(root, contract)

    def test_compile_never_executes_candidate_code(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "src").mkdir()
            (root / "src/agent.py").write_text("raise RuntimeError('not executed')", encoding="utf-8")
            CHECKS.compile_sources(root)
            self.assertEqual(list(root.rglob("*.pyc")), [])


if __name__ == "__main__":
    unittest.main()

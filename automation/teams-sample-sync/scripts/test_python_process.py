"""HTTP cleanup terminates listening workers as well as their parent process."""

import importlib.util
from pathlib import Path
import socket
import subprocess
import sys
import unittest
import time


SPEC = importlib.util.spec_from_file_location("python_process", Path(__file__).with_name("python_process.py"))
PROCESS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROCESS)


class ProcessTreeTests(unittest.TestCase):
    def test_cleanup_terminates_worker_descendants(self):
        self.check_worker_cleanup(parent_exits=False)

    def test_cleanup_terminates_worker_after_parent_exits(self):
        self.check_worker_cleanup(parent_exits=True)

    def check_worker_cleanup(self, parent_exits):
        worker = (
            "import socket,time; s=socket.socket(); s.bind(('127.0.0.1',0)); "
            "s.listen(); print(s.getsockname()[1],flush=True); time.sleep(30)"
        )
        parent = "import subprocess,sys,time; subprocess.Popen([sys.executable,'-c',sys.argv[1]]); time.sleep(float(sys.argv[2]))"
        child = PROCESS.start_process(
            [sys.executable, "-I", "-c", parent, worker, "0" if parent_exits else "30"],
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True,
        )
        try:
            port = int(child.stdout.readline().strip())
            with socket.create_connection(("127.0.0.1", port), timeout=2):
                pass
            if parent_exits:
                child.wait(timeout=5)
            PROCESS.stop_process(child)
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                try:
                    with socket.create_connection(("127.0.0.1", port), timeout=0.2):
                        pass
                except OSError:
                    break
                time.sleep(0.02)
            else:
                self.fail("HTTP worker remained alive after process-tree cleanup")
        finally:
            if child.poll() is None:
                PROCESS.stop_process(child)
            child.stdout.close()


if __name__ == "__main__":
    unittest.main()

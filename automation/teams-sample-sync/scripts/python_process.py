"""Start and terminate the complete process tree of a protected HTTP check."""

import os
import signal
import subprocess
import importlib.util
from pathlib import Path
import ctypes
import sys


CREATE_SUSPENDED = 0x00000004


def start_process(arguments, **options):
    if os.name != "nt":
        if sys.platform != "linux":
            raise OSError("Protected Python process ownership supports Linux and Windows")
        # Adopt orphaned workers while retaining the coordinator's process group.
        # A new session here would escape shared deadline cancellation.
        libc = ctypes.CDLL(None, use_errno=True)
        if libc.prctl(36, 1, 0, 0, 0) != 0:  # PR_SET_CHILD_SUBREAPER
            raise OSError(ctypes.get_errno(), "Cannot own protected Python descendants")
        return subprocess.Popen(arguments, **options)
    spec = importlib.util.spec_from_file_location("windows_job", Path(__file__).with_name("python_windows_job.py"))
    jobs = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(jobs)
    child = subprocess.Popen(arguments, creationflags=CREATE_SUSPENDED, **options)
    try:
        child.close_validation_job = jobs.attach_job(child)
    except BaseException:
        child.kill()
        child.wait(timeout=5)
        raise
    return child


def stop_process(child):
    if os.name == "nt":
        child.close_validation_job()
    else:
        children = {}
        for status in Path("/proc").glob("[0-9]*/status"):
            try:
                parent = next(line.split()[1] for line in status.read_text().splitlines() if line.startswith("PPid:"))
                children.setdefault(int(parent), []).append(int(status.parent.name))
            except (OSError, StopIteration):
                continue
        pending = list(children.get(os.getpid(), []))
        descendants = []
        while pending:
            process = pending.pop()
            descendants.append(process)
            pending.extend(children.get(process, []))
        for process in reversed(descendants):
            try:
                os.kill(process, signal.SIGKILL)
            except ProcessLookupError:
                pass
    if child.poll() is None:
        child.kill()
    child.wait(timeout=5)

"""Fingerprint a validation-owned environment without loading its installed code."""

from hashlib import sha256
import json
import os
from pathlib import Path
import sys


def environment_digest(root):
    # Graph SDK paths exceed MAX_PATH even when the environment root is short.
    # Use the same extended paths as installation for traversal and file reads.
    root = root.absolute()
    if os.name == "nt" and not str(root).startswith("\\\\?\\"):
        root = Path("\\\\?\\" + str(root))

    digest = sha256()
    for file in sorted(root.rglob("*")):
        if file.is_symlink():
            # POSIX venv interpreters normally link to the trusted base runtime.
            standard_library_link = file == root / "lib64" and file.resolve() == (root / "lib").resolve()
            if file.parent != root / "bin" and not standard_library_link:
                raise ValueError("Unexpected symlink in Python validation environment")
            content = str(file.resolve()).encode()
        elif file.is_file():
            content_hash = sha256()
            with file.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    content_hash.update(chunk)
            content = content_hash.digest()
        else:
            continue
        digest.update(file.relative_to(root).as_posix().encode())
        digest.update(b"\0")
        digest.update(content)
        digest.update(b"\0")
    return digest.hexdigest()


if __name__ == "__main__":
    root = Path(sys.argv[1]) if len(sys.argv) == 3 else Path(json.loads(sys.stdin.buffer.read(1024 * 1024))["root"])
    if not root.is_dir() or root.is_symlink():
        raise SystemExit("Python environment must be a real directory")
    result = json.dumps({"digest": environment_digest(root)})
    if len(sys.argv) == 3:
        Path(sys.argv[2]).write_text(result, encoding="utf-8")
    else:
        print(result)

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Package source and hash-pinned Linux wheels for reproducible App Service installs."""

import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
from zipfile import ZipFile, ZIP_DEFLATED


def main():
    root = Path(__file__).resolve().parents[1]
    output = root / "artifacts" / "app.zip"
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="deployment-", dir=root / "artifacts") as temporary:
        dependencies = Path(temporary)
        subprocess.run(
            [
                sys.executable,
                "-m",
                "pip",
                "download",
                "--only-binary=:all:",
                "--platform",
                "manylinux2014_x86_64",
                "--platform",
                "manylinux_2_28_x86_64",
                "--python-version",
                "312",
                "--implementation",
                "cp",
                "--abi",
                "cp312",
                "--dest",
                str(dependencies),
                "-r",
                "requirements.txt",
            ],
            cwd=root,
            check=True,
        )
        lock = ["--no-index", "--find-links artifacts/deployment-wheels", "--require-hashes"]
        resolved_wheels = sorted(dependencies.glob("*.whl"))
        for wheel in resolved_wheels:
            name, version = wheel.name.split("-")[:2]
            digest = hashlib.sha256(wheel.read_bytes()).hexdigest()
            lock.append(f"{name.replace('_', '-')}=={version} --hash=sha256:{digest}")
        with ZipFile(output, "w", ZIP_DEFLATED) as archive:
            archive.writestr("requirements.txt", "\n".join(lock) + "\n")
            for path in sorted((root / "src").glob("*.py")):
                archive.write(path, path.relative_to(root))
            for wheel in resolved_wheels:
                archive.write(wheel, f"artifacts/deployment-wheels/{wheel.name}")
    print(output)


if __name__ == "__main__":
    main()

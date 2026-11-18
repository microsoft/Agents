"""Validate one Teams sample in a fresh environment on the CI matrix runtime."""

from pathlib import Path
import shutil
import sys
import tempfile
import platform

from packaging.version import Version

import importlib.util


def policy(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(f"{name}.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


PROJECT = policy("python_project")
PREPARE = policy("python_prepare")
SDK = policy("python_sdk_policy")
DIGEST = policy("python_environment_digest")


def validate(root, contracts):
    declarations = PROJECT.REQUIREMENTS.parse_declarations(
        (root / "requirements.txt").read_text(encoding="utf-8")
    )
    packages = {}
    for name in SDK.SDK_PACKAGES:
        requirement = declarations.get(name)
        if requirement is None:
            raise ValueError(f"Missing required SDK declaration: {name}")
        pins = list(requirement.specifier)
        if len(pins) != 1 or pins[0].operator != "==":
            raise ValueError(f"SDK dependency must use an exact version: {name}")
        version = Version(pins[0].version)
        if version.major != 1 or version < Version("1.9.0") or version.is_prerelease or version.local:
            raise ValueError(f"Unsupported SDK version: {name}")
        packages[name] = str(version)
    evidence = PROJECT.inspect_project(root, packages)
    if evidence["errors"]:
        raise ValueError("; ".join(evidence["errors"]))
    # Checked-in lock evidence remains required even though matrix runs resolve
    # fresh wheels for their interpreter rather than rewriting synchronization output.
    checked_lock = (root / "requirements.lock").read_text(encoding="utf-8")
    locked = PREPARE.INSTALLED.locked_versions(checked_lock)
    if any(locked.get(name) != version for name, version in packages.items()):
        raise ValueError("Checked-in SDK lock differs from declarations")
    directory = Path(tempfile.mkdtemp(prefix="teams-python-ci-"))
    try:
        environment = PREPARE.prepare_environment(
            (root / "requirements.txt").read_text(encoding="utf-8"), packages, directory / "environment",
            lock_content=checked_lock if platform.python_version() == "3.11.14" else None,
        )
        environment_root = Path(environment["interpreter"]).parent.parent
        original_digest = DIGEST.environment_digest(environment_root)
        checks = Path(__file__).with_name("python_checks.py").resolve()
        for operation in ("compile", "import", "contracts", "smoke"):
            arguments = [environment["interpreter"], "-I", "-B", str(checks), operation, str(root)]
            if operation == "contracts":
                arguments.append(str(contracts / f"{root.name}.py"))
            PREPARE.run(arguments, directory)
        if DIGEST.environment_digest(environment_root) != original_digest:
            raise ValueError("Python validation environment changed during CI candidate execution")
    finally:
        shutil.rmtree(directory)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: python_sample_ci.py SAMPLE_ROOT PROTECTED_CONTRACT_DIRECTORY")
    validate(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())

"""Inspect the supported requirements-based sample layout without importing it."""

import importlib.util
import json
from pathlib import Path
import sys


def trusted_module(name):
    file = Path(__file__).with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(name, file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


REQUIREMENTS = trusted_module("python_requirements")
SOURCE = trusted_module("python_source")


def inspect_project(root, packages):
    try:
        return inspect_valid_project(root, packages)
    except (OSError, ValueError, SyntaxError, RecursionError) as error:
        # Candidate defects are validation evidence so the coordinator can repair them.
        return {"errors": [str(error)], "capabilities": {
            "errors": [str(error)], "bot": False, "composeExtensions": False,
            "files": False, "queryCommands": [], "actionCommands": [],
            "resourceSpecificPermissions": [],
        }}


def inspect_valid_project(root, packages):
    if root.is_symlink() or not root.is_dir():
        raise ValueError("Python project root must be a real directory")
    for name in ("pyproject.toml", "setup.py", "setup.cfg", "pip.conf", "pip.ini"):
        if (root / name).exists():
            raise ValueError(f"Unsupported project build or install configuration: {name}")
    for name in ("requirements.txt", "src/agent.py", "src/main.py"):
        file = root / name
        if not file.is_file() or file.is_symlink():
            raise ValueError(f"Python project requires a regular file: {name}")
    requirements_file = root / "requirements.txt"
    if requirements_file.stat().st_size > 128 * 1024:
        raise ValueError("Python dependency declarations exceed the size limit")
    errors = REQUIREMENTS.validate_sdk_declarations(
        requirements_file.read_text(encoding="utf-8"), packages
    )
    files = SOURCE.inspect_sources(root)
    facts = {
        "errors": [], "bot": False, "composeExtensions": False, "files": False,
        "queryCommands": [], "actionCommands": [], "resourceSpecificPermissions": [],
    }
    for file in files:
        observed = file["capabilities"]
        for name in ("errors", "queryCommands", "actionCommands", "resourceSpecificPermissions"):
            facts[name].extend(observed[name])
        for name in ("bot", "composeExtensions", "files"):
            facts[name] = facts[name] or observed[name]
        if "linkHandlerEvidence" in observed:
            facts["linkHandlerEvidence"] = observed["linkHandlerEvidence"]
    errors.extend(facts["errors"])
    if not facts["bot"]:
        errors.append("Python project must construct an Agents SDK application")
    return {"errors": errors, "capabilities": facts}


if __name__ == "__main__":
    try:
        payload = sys.stdin.buffer.read(1024 * 1024 + 1)
        if len(payload) > 1024 * 1024:
            raise ValueError("Python project inspection input exceeds the size limit")
        request = json.loads(payload)
        print(json.dumps(inspect_project(Path(request["root"]), request["packages"])))
    except (OSError, ValueError, TypeError, KeyError, SyntaxError, RecursionError) as error:
        print(f"Python project inspection failed: {error}", file=sys.stderr)
        sys.exit(1)

"""Resolve verified wheels using pinned trusted pip, then install into a fresh venv."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile


def load_policy(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(f"{name}.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


DECLARATIONS = load_policy("python_requirements")
LOCK = load_policy("python_lock")
INSTALLED = load_policy("python_installed")


def locked_resolution_input(requirements, lock_content):
    locked = INSTALLED.locked_artifacts(lock_content)
    extras = {}
    for name, requirement in DECLARATIONS.parse_declarations(requirements).items():
        if requirement.marker and not requirement.marker.evaluate():
            continue
        if name not in locked or not requirement.specifier.contains(locked[name][0]):
            raise ValueError(f"Committed lock does not satisfy declaration: {name}")
        extras[name] = sorted(requirement.extras)
    lines = []
    for name, (version, digest) in sorted(locked.items()):
        selected_extras = extras.get(name, [])
        suffix = "[" + ",".join(selected_extras) + "]" if selected_extras else ""
        lines.append(f"{name}{suffix}=={version} --hash=sha256:{digest}")
    return "\n".join(lines) + "\n"


def run(command, cwd):
    retained = {"PATH", "Path", "SystemRoot", "WINDIR", "TEMP", "TMP", "SSL_CERT_FILE"}
    retained = {key.casefold() for key in retained}
    environment = {key: value for key, value in os.environ.items() if key.casefold() in retained}
    subprocess.run(
        command, cwd=cwd, env=environment, check=True, timeout=180,
        stdin=subprocess.DEVNULL,
    )


def prepare_environment(requirements, packages, environment_root, lock_content=None):
    errors = DECLARATIONS.validate_sdk_declarations(requirements, packages)
    if errors:
        raise ValueError("; ".join(errors))
    # Caller supplies a newly allocated location outside candidate write scope.
    environment_root = Path(environment_root).absolute()
    if environment_root.exists():
        raise ValueError("Validation environment must not already exist")
    # Graph SDK generated wheel paths exceed MAX_PATH even in short environments.
    # Extended paths avoid changing machine policy and retain ordinary isolation.
    if os.name == "nt" and not str(environment_root).startswith("\\\\?\\"):
        environment_root = Path("\\\\?\\" + str(environment_root))
    import pip
    if pip.__version__ != "26.1.1":
        raise ValueError("Dependency preparation requires trusted pip 26.1.1")

    with tempfile.TemporaryDirectory(prefix="teams-python-resolve-") as directory:
        staging = Path(directory)
        declarations = staging / "requirements.txt"
        resolution_input = locked_resolution_input(requirements, lock_content) if lock_content is not None else requirements
        declarations.write_text(resolution_input, encoding="utf-8")
        report_file = staging / "report.json"
        base = [sys.executable, "-I", "-m", "pip"]
        options = [
            "--isolated", "--index-url", "https://pypi.org/simple",
            "--only-binary=:all:", "--disable-pip-version-check", "--no-cache-dir",
            "--retries", "0", "--timeout", "20",
            "--verbose",
        ]
        lock_options = ["--require-hashes"] if lock_content is not None else []
        run(base + ["install", *options, *lock_options, "--dry-run", "--ignore-installed", "--report",
                    str(report_file), "-r", str(declarations)], staging)
        lock = LOCK.lock_from_report(json.loads(report_file.read_text(encoding="utf-8")), packages)
        if lock_content is not None and INSTALLED.locked_artifacts(lock) != INSTALLED.locked_artifacts(lock_content):
            raise ValueError("Resolved wheels differ from the committed lock")
        lock_file = staging / "requirements.lock"
        lock_file.write_text(lock, encoding="utf-8")
        run([sys.executable, "-I", "-m", "venv", "--without-pip", str(environment_root)], staging)
        interpreter = environment_root / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
        run(base + ["--python", str(interpreter), "install", *options,
                    "--require-hashes", "--no-compile", "-r", str(lock_file)], staging)
        site = environment_root / (
            "Lib/site-packages" if os.name == "nt"
            else f"lib/python{sys.version_info.major}.{sys.version_info.minor}/site-packages"
        )
        errors = INSTALLED.check_installed(site, lock, packages)
        if errors:
            raise ValueError("; ".join(errors))
        run(base + ["--python", str(interpreter), "check", "--disable-pip-version-check"], staging)
        return {"lock": lock, "interpreter": str(interpreter), "sitePackages": str(site)}


if __name__ == "__main__":
    try:
        if len(sys.argv) != 3:
            raise ValueError("Usage: python_prepare.py REQUEST_JSON RESULT_JSON")
        request_file = Path(sys.argv[1])
        if request_file.stat().st_size > 1024 * 1024:
            raise ValueError("Python preparation input exceeds the size limit")
        request = json.loads(request_file.read_text(encoding="utf-8"))
        result = prepare_environment(
            request["requirements"], request["packages"], request["environmentRoot"]
        )
        Path(sys.argv[2]).write_text(json.dumps(result), encoding="utf-8")
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        print(f"Python dependency preparation failed: {error}", file=sys.stderr)
        sys.exit(1)

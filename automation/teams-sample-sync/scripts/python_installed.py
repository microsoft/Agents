"""Check environment distribution metadata without importing installed packages."""

from importlib.metadata import distributions
from pathlib import Path
import re

from packaging.utils import canonicalize_name


LOCK_LINE = re.compile(r"^([A-Za-z0-9_.-]+)==([^\s]+) --hash=sha256:([a-f0-9]{64})$")


def locked_artifacts(content):
    versions = {}
    for line in content.splitlines():
        if not line or line.startswith("#"):
            continue
        match = LOCK_LINE.fullmatch(line)
        if not match:
            raise ValueError("Invalid trusted Python lock format")
        name = canonicalize_name(match[1])
        if name in versions:
            raise ValueError(f"Duplicate locked distribution: {name}")
        versions[name] = (match[2], match[3])
    if not versions:
        raise ValueError("Python lock contains no distributions")
    return versions


def locked_versions(content):
    return {name: value[0] for name, value in locked_artifacts(content).items()}


def check_installed(site_packages, lock_content, selected_packages):
    site = Path(site_packages)
    if not site.is_dir() or site.is_symlink():
        raise ValueError("Installed distributions require a real site-packages directory")
    for file in site.rglob("*"):
        if file.is_symlink():
            raise ValueError(f"Installed environment contains a symlink: {file.name}")
        if file.suffix == ".pth":
            raise ValueError(f"Installed environment contains executable path configuration: {file.name}")
    expected = locked_versions(lock_content)
    errors = []
    observed = {}
    for distribution in distributions(path=[str(site)]):
        name = distribution.metadata.get("Name")
        if not name or not distribution.version:
            raise ValueError("Installed distribution metadata is incomplete")
        name = canonicalize_name(name)
        if name in observed:
            raise ValueError(f"Duplicate installed distribution: {name}")
        observed[name] = distribution.version
    for name, version in expected.items():
        if observed.get(name) != version:
            errors.append(f"Installed distribution differs from lock: {name}")
    for name in observed:
        if name not in expected:
            errors.append(f"Installed distribution is outside the lock: {name}")
    for name, version in selected_packages.items():
        if expected.get(name) != version or observed.get(name) != version:
            errors.append(f"Installed SDK differs from frozen selection: {name}")
    return errors

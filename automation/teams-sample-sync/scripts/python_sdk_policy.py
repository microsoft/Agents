"""Select a compatible SDK set from trusted release metadata without imports.

The transport supplies release-specific PyPI metadata. Full transitive wheel
resolution remains the responsibility of the pinned installation resolver.
"""

from hashlib import sha256
import itertools
import json
import sys
import platform

from packaging.requirements import Requirement
from packaging.specifiers import SpecifierSet
from packaging.utils import canonicalize_name
from packaging.version import Version


SDK_PACKAGES = (
    "microsoft-agents-activity",
    "microsoft-agents-hosting-core",
    "microsoft-agents-hosting-aiohttp",
    "microsoft-agents-authentication-msal",
    "microsoft-agents-hosting-msteams",
)
MAX_SELECTION_ATTEMPTS = 50_000


def target_environment(python_version):
    """Synchronization resolves for Linux CPython, independent of the host OS."""
    version = Version(python_version)
    if len(version.release) != 3 or version.is_prerelease or version.is_devrelease:
        raise ValueError("Python runtime must be an exact stable patch version")
    return {
        "implementation_name": "cpython",
        "implementation_version": python_version,
        "os_name": "posix",
        "platform_machine": "x86_64",
        "platform_python_implementation": "CPython",
        "platform_release": "",
        "platform_system": "Linux",
        "platform_version": "",
        "python_full_version": python_version,
        "python_version": ".".join(python_version.split(".")[:2]),
        "sys_platform": "linux",
    }


def validate_metadata(metadata):
    if not isinstance(metadata, dict):
        raise ValueError("Python release metadata must be an object")
    for field in ("name", "version", "requires_python"):
        if not isinstance(metadata.get(field), str) or not metadata[field]:
            raise ValueError(f"Python release metadata requires {field}")
    if not isinstance(metadata.get("yanked"), bool):
        raise ValueError("Python release metadata requires a boolean yanked field")
    requirements = metadata.get("requires_dist")
    if not isinstance(requirements, list) or any(not isinstance(item, str) for item in requirements):
        raise ValueError("Python release metadata requires a requires_dist string list")


def release_candidates(name, releases, minimum, python_version):
    candidates = []
    for metadata in releases:
        validate_metadata(metadata)
        if canonicalize_name(metadata["name"]) != name:
            raise ValueError(f"Release metadata name differs from {name}")
        version = Version(metadata["version"])
        if version.is_prerelease or version.is_devrelease or version.local:
            continue
        if version.major != minimum.major or version < minimum:
            continue
        if metadata["yanked"]:
            continue
        if not SpecifierSet(metadata["requires_python"]).contains(python_version):
            continue
        requirements = [Requirement(value) for value in metadata["requires_dist"]]
        if any(requirement.url for requirement in requirements):
            raise ValueError(f"Direct URL dependency in {name} release metadata")
        candidates.append((version, requirements))

    return sorted(candidates, key=lambda item: item[0], reverse=True)


def compatible_selection(selected, python_version):
    environment = target_environment(python_version)
    extras = {name: {""} for name in selected}
    # Requirements can activate extras on another selected SDK distribution.
    # Iterate to a fixed point so those extras' requirements are checked too.
    changed = True
    while changed:
        changed = False
        for name, (_, requirements) in selected.items():
            for requirement in requirements:
                if requirement.marker and not any(
                    requirement.marker.evaluate({**environment, "extra": extra})
                    for extra in extras[name]
                ):
                    continue
                dependency = canonicalize_name(requirement.name)
                if dependency not in selected:
                    continue
                if not requirement.specifier.contains(selected[dependency][0]):
                    return False
                activated = requirement.extras - extras[dependency]
                if activated:
                    extras[dependency].update(activated)
                    changed = True

    return True


def select_sdk_versions(minimum, python_version, feeds):
    minimum_version = Version(minimum)
    if minimum_version.is_prerelease or minimum_version.is_devrelease or minimum_version.local:
        raise ValueError("Python SDK minimum must be stable")
    target_environment(python_version)
    normalized = {canonicalize_name(name): releases for name, releases in feeds.items()}
    if len(normalized) != len(feeds) or set(normalized) != set(SDK_PACKAGES):
        raise ValueError("Python SDK metadata must contain exactly the required packages")

    available = [
        release_candidates(name, normalized[name], minimum_version, python_version)
        for name in SDK_PACKAGES
    ]
    for attempt, combination in enumerate(itertools.product(*available), start=1):
        if attempt > MAX_SELECTION_ATTEMPTS:
            raise ValueError("Python SDK compatibility search exceeded its limit")
        selected = dict(zip(SDK_PACKAGES, combination))
        if not compatible_selection(selected, python_version):
            continue
        packages = {name: str(value[0]) for name, value in selected.items()}
        identity = sha256(json.dumps(packages, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
        return {
            "minimumAgentsSdkVersion": minimum,
            "selectedAgentsSdkVersion": packages["microsoft-agents-hosting-core"],
            "releaseTag": f"pypi:{identity}",
            "packages": packages,
        }

    raise ValueError("No compatible stable Python SDK selection supports the configured runtime")


def assert_selection(selection, minimum, python_version):
    """Validate serialized policy evidence without consulting mutable package feeds."""
    if not isinstance(selection, dict) or not isinstance(selection.get("packages"), dict):
        raise ValueError("Python SDK selection is incomplete")
    packages = selection["packages"]
    if set(packages) != set(SDK_PACKAGES):
        raise ValueError("Python SDK selection has an incomplete package map")
    feeds = {
        name: [{
            "name": name,
            "version": version,
            "requires_python": f"=={python_version}",
            "requires_dist": [],
            "yanked": False,
        }]
        for name, version in packages.items()
    }
    expected = select_sdk_versions(minimum, python_version, feeds)
    if any(selection.get(key) != value for key, value in expected.items()):
        raise ValueError("Python SDK selection identity or minimum differs from policy")


if __name__ == "__main__":
    try:
        payload = sys.stdin.buffer.read(8 * 1024 * 1024 + 1)
        if len(payload) > 8 * 1024 * 1024:
            raise ValueError("Python SDK policy input exceeds its size limit")
        request = json.loads(payload)
        if platform.python_version() != request["pythonVersion"]:
            raise ValueError(
                f"Trusted Python runtime differs from configured runtime: {platform.python_version()}"
            )
        if request.get("operation") == "runtime":
            result = {"valid": True}
        elif request.get("operation") == "assert":
            assert_selection(request["selection"], request["minimum"], request["pythonVersion"])
            result = {"valid": True}
        elif request.get("operation") == "select":
            result = select_sdk_versions(request["minimum"], request["pythonVersion"], request["feeds"])
        else:
            raise ValueError("Unsupported Python SDK policy operation")
        print(json.dumps(result))
    except (ValueError, TypeError, KeyError) as error:
        print(f"Python SDK policy failed: {error}", file=sys.stderr)
        sys.exit(1)

"""Validate registry-only declarations before any dependency resolver runs."""

from packaging.requirements import Requirement
from packaging.utils import canonicalize_name


LEGACY_RUNTIMES = {
    "microsoft-teams-apps", "microsoft-teams-ai", "microsoft-teams-botbuilder",
    "botbuilder-core", "botbuilder-integration-aiohttp", "botbuilder-dialogs",
    "microsoft-agents-hosting-teams",
}


def parse_declarations(content):
    declarations = {}
    for number, raw in enumerate(content.splitlines(), start=1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        # Options and continuations are not part of this declaration format.
        # Lockfiles are separately produced and verified by trusted tooling.
        if line.startswith("-") or "\\" in line or " --" in line:
            raise ValueError(f"Unsupported requirement option on line {number}")
        requirement = Requirement(line)
        name = canonicalize_name(requirement.name)
        if requirement.url:
            raise ValueError(f"Direct URL/path requirement is forbidden: {name}")
        if name in declarations:
            raise ValueError(f"Duplicate dependency declaration: {name}")
        if name in LEGACY_RUNTIMES or name.startswith("botframework-"):
            raise ValueError(f"Legacy application runtime dependency: {name}")
        declarations[name] = requirement

    if not declarations:
        raise ValueError("Dependency declarations must not be empty")
    return declarations


def validate_sdk_declarations(content, packages):
    declarations = parse_declarations(content)
    errors = []
    for name, version in packages.items():
        requirement = declarations.get(name)
        if requirement is None:
            errors.append(f"Missing required SDK dependency: {name}")
            continue
        if requirement.marker is not None or requirement.extras:
            errors.append(f"Required SDK dependency must be unconditional without extras: {name}")
        specifiers = list(requirement.specifier)
        if len(specifiers) != 1 or specifiers[0].operator != "==" or specifiers[0].version != version:
            errors.append(f"{name} must declare frozen exact version {version}")
    unexpected = {
        name for name in declarations
        if name.startswith("microsoft-agents-") and name not in packages
    }
    for name in sorted(unexpected):
        errors.append(f"SDK dependency is outside the frozen selection: {name}")
    return errors

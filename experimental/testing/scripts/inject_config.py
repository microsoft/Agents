"""
inject_config.py — ${{ KEY }} template substitution for agent config files.

Variables are sourced from ./.env (KEY=VALUE pairs produced by
provisioning) plus any --var KEY=VALUE overrides on the command line.

Syntax:  ${{ KEY }}
  KEY  — key name from the .env file, or a dot-separated path for nested values
         set via --var.

Examples:
  ${{ APP_ID }}              → .env value for APP_ID
  ${{ auth.clientId }}       → nested value set via --var auth.clientId=...

Walks ./agents/<scenario>/ and processes every recognised template:
  env.TEMPLATE      → .env                 (same directory)
  appsettings.json  → appsettings.local.json (same directory, only when file
                       contains at least one ${{ }} placeholder)

Usage:
    uv run python scripts/inject_config.py \\
        --scenario quickstart \\
        --vars-file .env
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Any

PLACEHOLDER_RE = re.compile(r'\$\{\{\s*([\w.]+)\s*\}\}')

# Maps template filename → destination filename, relative to the template's own directory.
TEMPLATE_MAP: dict[str, str] = {
    'env.TEMPLATE': '.env',
    'appsettings.json': 'appsettings.local.json',
}


def main() -> None:
    """Parse arguments, load variables from the vars file, and run template substitution."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        '--scenario', default='*',
        help='Agent scenario glob under ./agents/ (default: *, all scenarios).',
    )
    parser.add_argument(
        '--vars-file', type=Path, default=Path(__file__).parent.parent / '.env',
        help='.env file containing variables for template substitution (default: <env-dir>/.env).',
    )
    parser.add_argument(
        '--var', action='append', default=[], metavar='KEY=VALUE',
        help='Override or add a variable. Dot-paths are supported: --var foo.bar=value. May be repeated.',
    )
    args = parser.parse_args()

    if not args.vars_file.exists():
        sys.exit(f"Vars file not found: {args.vars_file}")

    variables: dict[str, Any] = _load_env_file(args.vars_file)

    for kv in args.var:
        if '=' not in kv:
            parser.error(f"--var must be KEY=VALUE, got: {kv!r}")
        key, _, value = kv.partition('=')
        _set_path(variables, key.strip(), value)

    agents_dir = Path(__file__).parent.parent / 'agents'
    matches = sorted(p for p in agents_dir.glob(args.scenario) if p.is_dir())
    if not matches:
        sys.exit(f"No scenario directories matched: {args.scenario}")
    for scenario_dir in matches:
        _discover_and_process(scenario_dir, variables)

    print("Config injection complete.")


def _load_env_file(path: Path) -> dict[str, Any]:
    """Parse a KEY=VALUE .env file into a flat dict, skipping comments and blank lines."""
    result: dict[str, Any] = {}
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        if '=' in line:
            key, _, value = line.partition('=')
            result[key.strip()] = value.strip()
    return result


def _discover_and_process(scenario_dir: Path, variables: dict[str, Any]) -> None:
    """Walk scenario_dir and process every recognised template defined in TEMPLATE_MAP."""
    found = 0
    for template_name, dest_name in TEMPLATE_MAP.items():
        for template_path in sorted(scenario_dir.rglob(template_name)):
            content = template_path.read_text(encoding='utf-8')
            # Skip files that contain no placeholders (e.g. committed appsettings.json with no vars).
            if not PLACEHOLDER_RE.search(content):
                continue
            dest_path = template_path.parent / dest_name
            _process_file(template_path, dest_path, variables, _content=content)
            found += 1

    if found == 0:
        print(f"Warning: no templates with ${{{{ }}}} placeholders found under {scenario_dir}")


def _process_file(
    template: Path,
    dest: Path,
    variables: dict[str, Any],
    *,
    _content: str | None = None,
) -> None:
    """Substitute all ${{ KEY }} placeholders in template and write the result to dest."""
    content = _content if _content is not None else template.read_text(encoding='utf-8')

    unresolved: list[str] = []

    def replace(match: re.Match) -> str:  # type: ignore[type-arg]
        path = match.group(1)
        try:
            return str(_resolve_path(variables, path))
        except KeyError:
            unresolved.append(path)
            return match.group(0)

    rendered = PLACEHOLDER_RE.sub(replace, content)

    if unresolved:
        sys.exit(
            f"Template {template}: unresolved placeholder(s): {', '.join(unresolved)}\n"
            "Add the missing values via --var or ensure they appear in the vars file."
        )

    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(rendered, encoding='utf-8')
    print(f"  {template} -> {dest}")


def _resolve_path(data: dict[str, Any], path: str) -> Any:
    """Traverse a dot-separated path through a (possibly nested) dict."""
    current: Any = data
    for part in path.split('.'):
        if not isinstance(current, dict) or part not in current:
            raise KeyError(path)
        current = current[part]
    return current


def _set_path(data: dict[str, Any], path: str, value: str) -> None:
    """Write value at a dot-separated path, creating intermediate dicts as needed."""
    parts = path.split('.')
    current = data
    for part in parts[:-1]:
        current = current.setdefault(part, {})
    current[parts[-1]] = value


if __name__ == '__main__':
    main()

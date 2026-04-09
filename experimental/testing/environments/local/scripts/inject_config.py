"""
inject_config.py — Generic ${{ path }} template substitution for agent config files.

Variables are sourced from infra-outputs.json (a flat or nested JSON file produced by
provisioning) plus any --var KEY=VALUE overrides on the command line.

Syntax:  ${{ path }}
  path  — dot-separated key into the outputs JSON.

Examples:
  ${{ APP_ID }}              → outputs["APP_ID"]
  ${{ auth.clientId }}       → outputs["auth"]["clientId"]

Discovery mode (--scenario):
  Walks environments/local/agents/<scenario>/ and processes every recognised template:
    env.TEMPLATE      → .env                 (same directory)
    appsettings.json  → appsettings.local.json (same directory, only when file
                         contains at least one ${{ }} placeholder)

Single-file mode (--template + --output):
  Processes one template file explicitly.

Usage:
    uv run python scripts/inject_config.py \\
        --scenario quickstart \\
        --outputs-file .infra.json \\
        --var AUTH_TYPE=ClientSecret \\
        --var CLIENT_SECRET=<secret>
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

PLACEHOLDER_RE = re.compile(r'\$\{\{\s*([\w.]+)\s*\}\}')

# Maps template filename → output filename, relative to the template's own directory.
TEMPLATE_MAP: dict[str, str] = {
    'env.TEMPLATE': '.env',
    'appsettings.json': 'appsettings.local.json',
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--scenario', help='Agent scenario folder under environments/local/agents/. Discovers and processes all templates.')
    source.add_argument('--template', type=Path, help='Single template file to process.')
    parser.add_argument('--output', type=Path, help='Output file (required with --template).')
    parser.add_argument(
        '--outputs-file', type=Path, default=Path(__file__).parent.parent / '.infra.json',
        help='JSON file containing provisioned infrastructure outputs (default: <env-dir>/.infra.json).',
    )
    parser.add_argument(
        '--var', action='append', default=[], metavar='KEY=VALUE',
        help='Override or add a variable. Dot-paths are supported: --var foo.bar=value. May be repeated.',
    )
    args = parser.parse_args()

    if args.template and not args.output:
        parser.error('--output is required when using --template.')

    if not args.outputs_file.exists():
        sys.exit(f"Outputs file not found: {args.outputs_file}")

    variables: dict[str, Any] = json.loads(args.outputs_file.read_text(encoding='utf-8-sig'))

    # Apply --var overrides, supporting dot-path keys to set nested values.
    for kv in args.var:
        if '=' not in kv:
            parser.error(f"--var must be KEY=VALUE, got: {kv!r}")
        key, _, value = kv.partition('=')
        _set_path(variables, key.strip(), value)

    if args.template:
        _process_file(args.template, args.output, variables)
    else:
        scenario_dir = Path(__file__).parent.parent / 'agents' / args.scenario
        if not scenario_dir.is_dir():
            sys.exit(f"Scenario directory not found: {scenario_dir}")
        _discover_and_process(scenario_dir, variables)

    print("Config injection complete.")


def _discover_and_process(scenario_dir: Path, variables: dict[str, Any]) -> None:
    found = 0
    for template_name, output_name in TEMPLATE_MAP.items():
        for template_path in sorted(scenario_dir.rglob(template_name)):
            content = template_path.read_text(encoding='utf-8')
            # Skip files that contain no placeholders (e.g. committed appsettings.json with no vars).
            if not PLACEHOLDER_RE.search(content):
                continue
            output_path = template_path.parent / output_name
            _process_file(template_path, output_path, variables, _content=content)
            found += 1

    if found == 0:
        print(f"Warning: no templates with ${{{{ }}}} placeholders found under {scenario_dir}")


def _process_file(
    template: Path,
    output: Path,
    variables: dict[str, Any],
    *,
    _content: str | None = None,
) -> None:
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
            "Add the missing values via --var or ensure they appear in the outputs file."
        )

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(rendered, encoding='utf-8')
    print(f"  {template} → {output}")


def _resolve_path(data: dict[str, Any], path: str) -> Any:
    """Traverse a dot-separated path through a (possibly nested) dict."""
    current: Any = data
    for part in path.split('.'):
        if not isinstance(current, dict) or part not in current:
            raise KeyError(path)
        current = current[part]
    return current


def _set_path(data: dict[str, Any], path: str, value: str) -> None:
    """Write `value` at a dot-separated path, creating intermediate dicts as needed."""
    parts = path.split('.')
    current = data
    for part in parts[:-1]:
        current = current.setdefault(part, {})
    current[parts[-1]] = value


if __name__ == '__main__':
    main()

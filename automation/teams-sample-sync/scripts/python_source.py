"""Read Python syntax as data without importing candidate modules.

Run with ``python -I python_source.py PATH``. The JSON inventory preserves route
expressions for the trusted capability policy to interpret explicitly.
"""

import ast
import json
from pathlib import Path
import sys


MAX_SOURCE_BYTES = 1024 * 1024
MAX_SOURCE_FILES = 256
IGNORED_DIRECTORIES = {".venv", "venv", "__pycache__", ".pytest_cache", ".git"}


class CapabilityInspector(ast.NodeVisitor):
    """Recognize explicit SDK construction and routes with lexical aliases."""

    def __init__(self, file):
        self.file = file
        self.bindings = {}
        self.facts = {
            "errors": [], "bot": False, "composeExtensions": False,
            "files": False, "queryCommands": [], "actionCommands": [],
            "resourceSpecificPermissions": [],
        }

    def expression(self, node):
        if isinstance(node, ast.Name):
            binding = self.bindings.get(node.id, node.id)
            return binding if isinstance(binding, str) else "<literal>"
        if isinstance(node, ast.Attribute):
            return f"{self.expression(node.value)}.{node.attr}"
        if isinstance(node, ast.Subscript):
            return self.expression(node.value)
        return "<dynamic>"

    def visit_ImportFrom(self, node):
        module = node.module or ""
        if module.startswith(("botbuilder", "botframework", "microsoft_teams.apps")):
            self.facts["errors"].append(f"Legacy application import in {self.file}: {module}")
        for item in node.names:
            if item.name == "*":
                self.facts["errors"].append(f"Unsupported wildcard import in {self.file}")
            self.bindings[item.asname or item.name] = f"{module}.{item.name}"

    def visit_Import(self, node):
        for item in node.names:
            if item.name.startswith(("botbuilder", "botframework", "microsoft_teams.apps")):
                self.facts["errors"].append(f"Legacy application import in {self.file}: {item.name}")
            self.bindings[item.asname or item.name.split(".")[0]] = (
                item.name if item.asname else item.name.split(".")[0]
            )

    def visit_AnnAssign(self, node):
        if node.value is not None:
            assignment = ast.copy_location(ast.Assign(targets=[node.target], value=node.value), node)
            self.visit_Assign(assignment)

    def visit_Assign(self, node):
        value = node.value
        binding = self.expression(value)
        if isinstance(value, ast.Call):
            constructor = self.expression(value.func)
            if constructor == "microsoft_agents.hosting.core.AgentApplication":
                self.facts["bot"] = True
                binding = "<agent>"
            elif constructor == "microsoft_agents.hosting.msteams.TeamsAgentExtension":
                if not value.args or self.expression(value.args[0]) != "<agent>":
                    self.facts["errors"].append(f"Unresolved Teams application in {self.file}:{node.lineno}")
                binding = "<teams>"
        if isinstance(value, ast.Constant) and isinstance(value.value, str):
            binding = ("literal", value.value)
        for target in node.targets:
            if isinstance(target, ast.Name):
                self.bindings[target.id] = binding
        self.generic_visit(node)

    def visit_Call(self, node):
        route = self.expression(node.func)
        evidence = f"{self.file}:{node.lineno}"
        if route.startswith("<parameter>.") and (
            any(f".{area}." in route for area in ("message_extensions", "file_consent", "meetings"))
            or route.rsplit(".", 1)[-1] in {"activity", "message", "add_route"}
        ):
            self.facts["errors"].append(f"Unresolved SDK registration through helper parameter at {evidence}")
        if route.startswith("<teams>.message_extensions."):
            self.facts["composeExtensions"] = True
            method = route.rsplit(".", 1)[1]
            if method in {"query", "fetch_action", "submit_action"}:
                selector = node.args[0] if node.args else next(
                    (item.value for item in node.keywords if item.arg == "command_id"), None
                )
                value = selector.value if isinstance(selector, ast.Constant) else None
                if isinstance(selector, ast.Name):
                    binding = self.bindings.get(selector.id)
                    if isinstance(binding, tuple):
                        value = binding[1]
                if not isinstance(value, str) or not value:
                    self.facts["errors"].append(f"Unsupported dynamic command selector at {evidence}")
                else:
                    key = "queryCommands" if method == "query" else "actionCommands"
                    self.facts[key].append({"id": value, "evidence": evidence})
            elif method in {"query_link", "anonymous_query_link"}:
                self.facts["linkHandlerEvidence"] = evidence
            else:
                self.facts["errors"].append(f"Unsupported message-extension registration at {evidence}")
        elif route.startswith("<teams>.file_consent."):
            self.facts["files"] = True
        elif route.startswith("<teams>.meetings."):
            permissions = self.facts["resourceSpecificPermissions"]
            permission = (
                "OnlineMeetingParticipant.Read.Chat" if "participants" in route
                else "OnlineMeeting.ReadBasic.Chat"
            )
            if permission not in permissions:
                permissions.append(permission)
        elif route == "<agent>.add_route" or (
            route == "getattr" and node.args
            and self.expression(node.args[0]).startswith(("<teams>", "<agent>"))
        ):
            self.facts["errors"].append(f"Unsupported dynamic registration at {evidence}")
        self.generic_visit(node)

    def visit_FunctionDef(self, node):
        for decorator in node.decorator_list:
            self.visit(decorator)
        previous = self.bindings
        self.bindings = previous.copy()
        for argument in (*node.args.posonlyargs, *node.args.args, *node.args.kwonlyargs):
            self.bindings[argument.arg] = "<parameter>"
        for statement in node.body:
            self.visit(statement)
        self.bindings = previous

    visit_AsyncFunctionDef = visit_FunctionDef


def inspect_file(file: Path, root: Path) -> dict:
    if file.is_symlink():
        raise ValueError(f"Python source must not be a symlink: {file}")

    content = file.read_bytes()
    if len(content) > MAX_SOURCE_BYTES:
        raise ValueError(f"Python source exceeds the size limit: {file}")

    tree = ast.parse(content, filename=str(file))
    inspector = CapabilityInspector(file.relative_to(root).as_posix())
    inspector.visit(tree)
    imports = []
    calls = []
    decorators = []
    for node in ast.walk(tree):
        if isinstance(node, ast.ImportFrom):
            imports.append({
                "module": node.module,
                "level": node.level,
                "names": [{"name": item.name, "alias": item.asname} for item in node.names],
            })
        elif isinstance(node, ast.Import):
            imports.append({
                "module": None,
                "level": 0,
                "names": [{"name": item.name, "alias": item.asname} for item in node.names],
            })
        elif isinstance(node, ast.Call):
            calls.append({"expression": ast.unparse(node), "line": node.lineno})
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            for decorator in node.decorator_list:
                decorators.append({
                    "owner": node.name,
                    "expression": ast.unparse(decorator),
                    "line": decorator.lineno,
                })

    return {
        "path": file.relative_to(root).as_posix(),
        "imports": imports,
        "calls": calls,
        "decorators": decorators,
        "capabilities": inspector.facts,
    }


def inspect_sources(root: Path) -> list[dict]:
    if root.is_symlink() or not root.is_dir():
        raise ValueError("Python source root must be a real directory")

    files = []
    # Walk explicitly so symlink directories cannot conceal executable source.
    pending = [root]
    while pending:
        directory = pending.pop()
        for entry in sorted(directory.iterdir()):
            if entry.name in IGNORED_DIRECTORIES:
                continue
            if entry.is_symlink():
                raise ValueError(f"Python source tree must not contain symlinks: {entry}")
            if entry.is_dir():
                pending.append(entry)
            elif entry.suffix == ".py":
                files.append(entry)
                if len(files) > MAX_SOURCE_FILES:
                    raise ValueError("Python source tree exceeds the file limit")

    if not files:
        raise ValueError("Python source tree contains no Python files")

    return [inspect_file(file, root) for file in sorted(files)]


if __name__ == "__main__":
    try:
        if len(sys.argv) != 2:
            raise ValueError("Usage: python -I python_source.py SOURCE_ROOT")
        print(json.dumps({"version": 1, "files": inspect_sources(Path(sys.argv[1]))}))
    except (OSError, SyntaxError, ValueError, RecursionError) as error:
        print(f"Cannot inspect Python source: {error}", file=sys.stderr)
        sys.exit(1)

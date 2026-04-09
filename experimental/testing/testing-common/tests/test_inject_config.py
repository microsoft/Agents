"""Unit tests for scripts/inject_config.py."""

import json
import sys
from pathlib import Path

import pytest

import inject_config
from inject_config import (
    PLACEHOLDER_RE,
    TEMPLATE_MAP,
    _discover_and_process,
    _process_file,
    _resolve_path,
    _set_path,
)


# ---------------------------------------------------------------------------
# _resolve_path
# ---------------------------------------------------------------------------

class TestResolvePath:
    def test_flat_key(self):
        assert _resolve_path({"APP_ID": "abc"}, "APP_ID") == "abc"

    def test_nested_key(self):
        data = {"auth": {"clientId": "xyz"}}
        assert _resolve_path(data, "auth.clientId") == "xyz"

    def test_deeply_nested(self):
        data = {"a": {"b": {"c": "deep"}}}
        assert _resolve_path(data, "a.b.c") == "deep"

    def test_missing_top_level_raises(self):
        with pytest.raises(KeyError):
            _resolve_path({}, "MISSING")

    def test_missing_nested_raises(self):
        with pytest.raises(KeyError):
            _resolve_path({"a": {}}, "a.b")

    def test_non_dict_intermediate_raises(self):
        with pytest.raises(KeyError):
            _resolve_path({"a": "string"}, "a.b")

    def test_empty_string_value(self):
        assert _resolve_path({"KEY": ""}, "KEY") == ""


# ---------------------------------------------------------------------------
# _set_path
# ---------------------------------------------------------------------------

class TestSetPath:
    def test_flat_key_new(self):
        data: dict = {}
        _set_path(data, "FOO", "bar")
        assert data == {"FOO": "bar"}

    def test_flat_key_overwrite(self):
        data = {"FOO": "old"}
        _set_path(data, "FOO", "new")
        assert data["FOO"] == "new"

    def test_nested_key_creates_intermediate(self):
        data: dict = {}
        _set_path(data, "a.b.c", "val")
        assert data == {"a": {"b": {"c": "val"}}}

    def test_nested_key_preserves_siblings(self):
        data = {"a": {"x": 1}}
        _set_path(data, "a.y", "2")
        assert data["a"]["x"] == 1
        assert data["a"]["y"] == "2"

    def test_overwrites_nested_value(self):
        data = {"a": {"b": "old"}}
        _set_path(data, "a.b", "new")
        assert data["a"]["b"] == "new"


# ---------------------------------------------------------------------------
# PLACEHOLDER_RE
# ---------------------------------------------------------------------------

class TestPlaceholderRegex:
    def test_simple_match(self):
        assert PLACEHOLDER_RE.search("${{ APP_ID }}")

    def test_no_spaces(self):
        assert PLACEHOLDER_RE.search("${{APP_ID}}")

    def test_dot_path(self):
        m = PLACEHOLDER_RE.search("${{ auth.clientId }}")
        assert m and m.group(1) == "auth.clientId"

    def test_no_match_on_plain_text(self):
        assert not PLACEHOLDER_RE.search("hello world")

    def test_captures_path_group(self):
        m = PLACEHOLDER_RE.search("prefix ${{ MY.KEY }} suffix")
        assert m and m.group(1) == "MY.KEY"

    def test_multiple_placeholders(self):
        matches = PLACEHOLDER_RE.findall("${{ A }} and ${{ B }}")
        assert matches == ["A", "B"]


# ---------------------------------------------------------------------------
# _process_file
# ---------------------------------------------------------------------------

class TestProcessFile:
    def test_substitutes_single_placeholder(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("CLIENT_ID=${{ APP_ID }}\n")
        output = tmp_path / ".env"

        _process_file(template, output, {"APP_ID": "abc123"})

        assert output.read_text() == "CLIENT_ID=abc123\n"

    def test_substitutes_multiple_placeholders(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("ID=${{ APP_ID }}\nTENANT=${{ TENANT_ID }}\n")
        output = tmp_path / ".env"

        _process_file(template, output, {"APP_ID": "id1", "TENANT_ID": "t1"})

        lines = output.read_text().splitlines()
        assert lines[0] == "ID=id1"
        assert lines[1] == "TENANT=t1"

    def test_substitutes_nested_path(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("VAL=${{ auth.clientId }}\n")
        output = tmp_path / ".env"

        _process_file(template, output, {"auth": {"clientId": "nested-val"}})

        assert output.read_text() == "VAL=nested-val\n"

    def test_accepts_precached_content(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("ORIGINAL=${{ X }}\n")
        output = tmp_path / ".env"

        # Pass different content via _content — template file content should be ignored.
        _process_file(template, output, {"X": "overridden"}, _content="X=${{ X }}\n")

        assert output.read_text() == "X=overridden\n"

    def test_creates_parent_directories(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("K=${{ K }}\n")
        output = tmp_path / "sub" / "dir" / ".env"

        _process_file(template, output, {"K": "v"})

        assert output.exists()

    def test_exits_on_unresolved_placeholder(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("K=${{ MISSING }}\n")
        output = tmp_path / ".env"

        with pytest.raises(SystemExit):
            _process_file(template, output, {})

    def test_unresolved_error_names_the_key(self, tmp_path, capsys):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("K=${{ MISSING_KEY }}\n")
        output = tmp_path / ".env"

        with pytest.raises(SystemExit) as exc_info:
            _process_file(template, output, {})

        # sys.exit() with a string sets that string as the exception value
        assert "MISSING_KEY" in str(exc_info.value)

    def test_empty_string_placeholder_is_valid(self, tmp_path):
        template = tmp_path / "env.TEMPLATE"
        template.write_text("SECRET=${{ CLIENT_SECRET }}\n")
        output = tmp_path / ".env"

        _process_file(template, output, {"CLIENT_SECRET": ""})

        assert output.read_text() == "SECRET=\n"


# ---------------------------------------------------------------------------
# _discover_and_process
# ---------------------------------------------------------------------------

class TestDiscoverAndProcess:
    def _make_scenario(self, root: Path) -> Path:
        """Create a minimal scenario directory tree under root."""
        scenario = root / "environments" / "local" / "agents" / "quickstart"
        py_dir = scenario / "python"
        py_dir.mkdir(parents=True)
        js_dir = scenario / "js"
        js_dir.mkdir(parents=True)
        return scenario

    def test_discovers_env_template(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        scenario = self._make_scenario(tmp_path)
        (scenario / "python" / "env.TEMPLATE").write_text("ID=${{ APP_ID }}\n")
        variables = {"APP_ID": "test-app-id"}

        _discover_and_process(scenario, variables)

        output = scenario / "python" / ".env"
        assert output.read_text() == "ID=test-app-id\n"

    def test_discovers_appsettings_with_placeholder(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        scenario = self._make_scenario(tmp_path)
        net_dir = scenario / "net"
        net_dir.mkdir()
        (net_dir / "appsettings.json").write_text('{"ClientId": "${{ APP_ID }}"}\n')
        variables = {"APP_ID": "my-app"}

        _discover_and_process(scenario, variables)

        output = net_dir / "appsettings.local.json"
        assert json.loads(output.read_text())["ClientId"] == "my-app"

    def test_skips_appsettings_without_placeholders(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        scenario = self._make_scenario(tmp_path)
        net_dir = scenario / "net"
        net_dir.mkdir()
        (net_dir / "appsettings.json").write_text('{"Logging": {"Default": "Information"}}\n')

        _discover_and_process(scenario, {})

        assert not (net_dir / "appsettings.local.json").exists()

    def test_processes_multiple_language_dirs(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        scenario = self._make_scenario(tmp_path)
        (scenario / "python" / "env.TEMPLATE").write_text("A=${{ A }}\n")
        (scenario / "js" / "env.TEMPLATE").write_text("B=${{ B }}\n")
        variables = {"A": "alpha", "B": "beta"}

        _discover_and_process(scenario, variables)

        assert (scenario / "python" / ".env").read_text() == "A=alpha\n"
        assert (scenario / "js" / ".env").read_text() == "B=beta\n"

    def test_warns_when_no_templates_found(self, tmp_path, monkeypatch, capsys):
        monkeypatch.chdir(tmp_path)
        scenario = self._make_scenario(tmp_path)

        _discover_and_process(scenario, {})

        captured = capsys.readouterr()
        assert "Warning" in captured.out


# ---------------------------------------------------------------------------
# main() — CLI integration
# ---------------------------------------------------------------------------

class TestMain:
    def test_single_file_mode(self, tmp_path, monkeypatch):
        outputs = tmp_path / "outputs.json"
        outputs.write_text(json.dumps({"APP_ID": "cli-app-id"}))
        template = tmp_path / "env.TEMPLATE"
        template.write_text("ID=${{ APP_ID }}\n")
        output = tmp_path / ".env"

        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--template", str(template), "--output", str(output),
             "--outputs-file", str(outputs)],
        )
        inject_config.main()

        assert output.read_text() == "ID=cli-app-id\n"

    def test_var_override(self, tmp_path, monkeypatch):
        outputs = tmp_path / "outputs.json"
        outputs.write_text(json.dumps({"APP_ID": "from-file"}))
        template = tmp_path / "env.TEMPLATE"
        template.write_text("ID=${{ APP_ID }}\n")
        output = tmp_path / ".env"

        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--template", str(template), "--output", str(output),
             "--outputs-file", str(outputs), "--var", "APP_ID=overridden"],
        )
        inject_config.main()

        assert output.read_text() == "ID=overridden\n"

    def test_var_dot_path_override(self, tmp_path, monkeypatch):
        outputs = tmp_path / "outputs.json"
        outputs.write_text(json.dumps({}))
        template = tmp_path / "env.TEMPLATE"
        template.write_text("V=${{ auth.clientId }}\n")
        output = tmp_path / ".env"

        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--template", str(template), "--output", str(output),
             "--outputs-file", str(outputs), "--var", "auth.clientId=dotval"],
        )
        inject_config.main()

        assert output.read_text() == "V=dotval\n"

    def test_missing_outputs_file_exits(self, tmp_path, monkeypatch):
        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--scenario", "quickstart",
             "--outputs-file", str(tmp_path / "nonexistent.json")],
        )
        with pytest.raises(SystemExit):
            inject_config.main()

    def test_template_without_output_exits(self, tmp_path, monkeypatch):
        outputs = tmp_path / "outputs.json"
        outputs.write_text("{}")
        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--template", str(tmp_path / "t.TEMPLATE"),
             "--outputs-file", str(outputs)],
        )
        with pytest.raises(SystemExit):
            inject_config.main()

    def test_scenario_mode(self, tmp_path, monkeypatch):
        outputs = tmp_path / "outputs.json"
        outputs.write_text(json.dumps({"APP_ID": "scenario-id"}))

        scenario_dir = tmp_path / "environments" / "local" / "agents" / "myscenario" / "python"
        scenario_dir.mkdir(parents=True)
        (scenario_dir / "env.TEMPLATE").write_text("ID=${{ APP_ID }}\n")

        monkeypatch.chdir(tmp_path)
        monkeypatch.setattr(
            sys, "argv",
            ["inject_config.py", "--scenario", "myscenario", "--outputs-file", str(outputs)],
        )
        inject_config.main()

        assert (scenario_dir / ".env").read_text() == "ID=scenario-id\n"

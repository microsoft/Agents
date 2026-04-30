import pathlib
from unittest.mock import patch

import pytest

from testing_common.types import SDKVersion
from testing_common.utils import create_agent_path, create_scenario
from testing_common.source_scenario import SourceScenario


def _make_agent_tree(root: pathlib.Path, agent: str, sdk: SDKVersion) -> pathlib.Path:
    """Create a minimal agent directory tree and return the run_agent.ps1 path."""
    script = root / "environments" / "local" / "agents" / agent / sdk.value / "run_agent.ps1"
    script.parent.mkdir(parents=True)
    script.write_text("# stub")
    return script


class TestCreateAgentPath:
    def test_returns_absolute_path_to_script(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        script = _make_agent_tree(tmp_path, "quickstart", SDKVersion.PYTHON)
        result = create_agent_path("quickstart", SDKVersion.PYTHON)

        assert pathlib.Path(result) == script.resolve()

    def test_raises_if_script_missing(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        with pytest.raises(FileNotFoundError, match="run_agent.ps1"):
            create_agent_path("nonexistent", SDKVersion.PYTHON)

    def test_all_sdk_versions(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        for sdk in SDKVersion:
            _make_agent_tree(tmp_path, "quickstart", sdk)
            path = create_agent_path("quickstart", sdk)
            assert sdk.value in path


class TestCreateScenario:
    def test_returns_source_scenario(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        _make_agent_tree(tmp_path, "quickstart", SDKVersion.PYTHON)
        scenario = create_scenario("quickstart", SDKVersion.PYTHON)

        assert isinstance(scenario, SourceScenario)

    def test_default_delay(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        _make_agent_tree(tmp_path, "quickstart", SDKVersion.PYTHON)
        scenario = create_scenario("quickstart", SDKVersion.PYTHON)

        assert scenario._delay == 30.0

    def test_custom_delay(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)
        monkeypatch.setenv("TEST_ENVIRONMENT", "local")

        import importlib, testing_common.constants as c
        importlib.reload(c)

        _make_agent_tree(tmp_path, "quickstart", SDKVersion.PYTHON)
        scenario = create_scenario("quickstart", SDKVersion.PYTHON, delay=5.0)

        assert scenario._delay == 5.0

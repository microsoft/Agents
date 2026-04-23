import pathlib
from unittest.mock import patch

import pytest


class TestAgentsPath:
    def test_default_uses_local_environment(self):
        # Re-import inside the test to pick up a clean module state.
        import importlib
        import testing_common.constants as c
        importlib.reload(c)

        expected = pathlib.Path.cwd() / "environments" / "local" / "agents"
        assert c.AGENTS_PATH == expected

    def test_env_var_overrides_environment_name(self, monkeypatch):
        monkeypatch.setenv("TEST_ENVIRONMENT", "cloud")

        import importlib
        import testing_common.constants as c
        importlib.reload(c)

        expected = pathlib.Path.cwd() / "environments" / "cloud" / "agents"
        assert c.AGENTS_PATH == expected

    def test_restores_to_local_after_env_var_removed(self, monkeypatch):
        monkeypatch.delenv("TEST_ENVIRONMENT", raising=False)

        import importlib
        import testing_common.constants as c
        importlib.reload(c)

        assert "local" in str(c.AGENTS_PATH)


class TestOtherConstants:
    def test_entry_point_name(self):
        from testing_common.constants import ENTRY_POINT_NAME
        assert ENTRY_POINT_NAME == "run_agent.ps1"

    def test_default_local_agent_endpoint(self):
        from testing_common.constants import DEFAULT_LOCAL_AGENT_ENDPOINT
        assert DEFAULT_LOCAL_AGENT_ENDPOINT == "http://localhost:3978/api/messages"

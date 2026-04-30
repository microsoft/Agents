import pathlib
from unittest.mock import patch, MagicMock

import pytest

from testing_common.source_scenario import SourceScenario
from testing_common.constants import DEFAULT_LOCAL_AGENT_ENDPOINT


class TestSourceScenarioInit:
    def test_stores_script_path(self, tmp_path):
        script = tmp_path / "run_agent.ps1"
        scenario = SourceScenario(str(script))
        assert scenario._script_path == pathlib.Path(str(script))

    def test_default_delay_is_zero(self, tmp_path):
        scenario = SourceScenario(str(tmp_path / "run_agent.ps1"))
        assert scenario._delay == 0.0

    def test_custom_delay(self, tmp_path):
        scenario = SourceScenario(str(tmp_path / "run_agent.ps1"), delay=15.0)
        assert scenario._delay == 15.0

    def test_uses_default_endpoint(self, tmp_path):
        scenario = SourceScenario(str(tmp_path / "run_agent.ps1"))
        # ExternalScenario stores the endpoint; access via the attribute set by super().__init__
        assert scenario._endpoint == DEFAULT_LOCAL_AGENT_ENDPOINT

    def test_accepts_none_config(self, tmp_path):
        scenario = SourceScenario(str(tmp_path / "run_agent.ps1"), config=None)
        assert scenario is not None


class TestRunScriptPwshDetection:
    def test_raises_when_no_powershell_found(self, tmp_path):
        script = tmp_path / "run_agent.ps1"
        script.write_text("# stub")
        scenario = SourceScenario(str(script))

        with patch("shutil.which", return_value=None):
            import asyncio

            async def _run():
                async with scenario._run_script():
                    pass

            with pytest.raises(FileNotFoundError, match="pwsh"):
                asyncio.get_event_loop().run_until_complete(_run())

    def test_prefers_pwsh_over_powershell(self, tmp_path):
        script = tmp_path / "run_agent.ps1"
        script.write_text("# stub")
        scenario = SourceScenario(str(script))

        captured = {}

        def _fake_which(name):
            if name == "pwsh":
                return "/usr/bin/pwsh"
            return None

        mock_process = MagicMock()
        mock_process.terminate = MagicMock()

        with patch("shutil.which", side_effect=_fake_which), \
             patch("subprocess.Popen", return_value=mock_process) as mock_popen, \
             patch("asyncio.sleep"):
            import asyncio

            async def _run():
                async with scenario._run_script():
                    captured["cmd"] = mock_popen.call_args[0][0]

            asyncio.get_event_loop().run_until_complete(_run())

        assert captured["cmd"][0] == "/usr/bin/pwsh"

    def test_falls_back_to_powershell(self, tmp_path):
        script = tmp_path / "run_agent.ps1"
        script.write_text("# stub")
        scenario = SourceScenario(str(script))

        captured = {}

        def _fake_which(name):
            if name == "powershell":
                return "C:\\Windows\\powershell.exe"
            return None

        mock_process = MagicMock()
        mock_process.terminate = MagicMock()

        with patch("shutil.which", side_effect=_fake_which), \
             patch("subprocess.Popen", return_value=mock_process) as mock_popen, \
             patch("asyncio.sleep"):
            import asyncio

            async def _run():
                async with scenario._run_script():
                    captured["cmd"] = mock_popen.call_args[0][0]

            asyncio.get_event_loop().run_until_complete(_run())

        assert captured["cmd"][0] == "C:\\Windows\\powershell.exe"

    def test_script_cwd_is_parent_directory(self, tmp_path):
        agent_dir = tmp_path / "my_agent"
        agent_dir.mkdir()
        script = agent_dir / "run_agent.ps1"
        script.write_text("# stub")
        scenario = SourceScenario(str(script))

        mock_process = MagicMock()
        mock_process.terminate = MagicMock()

        with patch("shutil.which", return_value="/usr/bin/pwsh"), \
             patch("subprocess.Popen", return_value=mock_process) as mock_popen, \
             patch("asyncio.sleep"):
            import asyncio

            async def _run():
                async with scenario._run_script():
                    pass

            asyncio.get_event_loop().run_until_complete(_run())

        _, kwargs = mock_popen.call_args
        assert kwargs["cwd"] == script.resolve().parent

import asyncio
import shutil
import subprocess

from pathlib import Path

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from microsoft_agents.testing import (
    ActivityTemplate,
    ClientConfig,
    ExternalScenario,
    ScenarioConfig,
)
from microsoft_agents.testing.core import ClientFactory

from .constants import DEFAULT_LOCAL_AGENT_ENDPOINT

_TEMPLATE = {
    "channel_id": "webchat",
    "locale": "en-US",
    "conversation": {"id": "conv1"},
    "from": {"id": "user1", "name": "User"},
    "recipient": {"id": "bot", "name": "Bot"},
}

client_config=ClientConfig(
    activity_template=ActivityTemplate(_TEMPLATE)
)

class SourceScenario(ExternalScenario):
    """Base class for script-based test scenarios."""

    def __init__(
        self,
        script_path: str,
        delay: float = 0.0,
        config: ScenarioConfig | None = None
    ) -> None:
        super().__init__(DEFAULT_LOCAL_AGENT_ENDPOINT, config)
        self._script_path = Path(script_path)
        self._delay = delay

    @asynccontextmanager
    async def _run_script(self) -> AsyncIterator[None]:

        script_path = self._script_path.resolve()

        runner = shutil.which("pwsh") or shutil.which("powershell")
        if runner is None:
            raise FileNotFoundError("Could not find pwsh or powershell in PATH")

        try:
            process = subprocess.Popen(
                [runner, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script_path)],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                cwd=script_path.parent,
                shell=True, # Needed to ensure the process group is correctly set up for termination
            )

            # wait for the agent to start running
            await asyncio.sleep(self._delay)

            yield

            process.terminate()
        except Exception as ex:
            process.kill()
            raise ex

    @asynccontextmanager
    async def run(self) -> AsyncIterator[ClientFactory]:
        """Start callback server and yield a client factory."""
        async with self._run_script():
            async with super().run() as factory:
                yield factory
from pathlib import Path

from microsoft_agents.testing import Scenario

from . import constants
from .source_scenario import SourceScenario
from .types import SDKVersion

def create_agent_path(agents_dir_path: Path, agent_name: str, sdk_version: SDKVersion) -> str:
    
    agent_path = agents_dir_path / agent_name / sdk_version.value / constants.ENTRY_POINT_NAME
    if not agent_path.exists():
        raise FileNotFoundError(f"Agent path does not exist: {agent_path}")

    return str(agent_path.resolve())

def create_scenario(agents_dir_path: str | Path, agent_name: str, sdk_version: SDKVersion, delay: float = 30.0) -> Scenario:
    
    agents_dir_path = Path(agents_dir_path)
    agent_path = create_agent_path(agents_dir_path, agent_name, sdk_version)
    return SourceScenario(
        agent_path,
        delay=delay,
    )
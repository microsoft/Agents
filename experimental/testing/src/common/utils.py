from microsoft_agents.testing import Scenario

from . import constants
from .source_scenario import SourceScenario
from .types import SDKVersion

def create_agent_path(agent_name: str, sdk_version: SDKVersion) -> str:
    
    agent_path = constants.AGENTS_PATH / agent_name / sdk_version.value / constants.ENTRY_POINT_NAME
    if not agent_path.exists():
        raise FileNotFoundError(f"Agent path does not exist: {agent_path}")

    return str(agent_path.resolve())

def create_scenario(agent_name: str, sdk_version: SDKVersion, delay: float = 5.0) -> Scenario:
    
    agent_path = create_agent_path(agent_name, sdk_version)
    return SourceScenario(
        agent_path,
        delay=delay,
    )
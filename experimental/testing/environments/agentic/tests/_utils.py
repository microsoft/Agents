from pathlib import Path

from microsoft_agents.testing import Scenario
from microsoft_agents.testing.cross_sdk import (
    create_scenario,
    SDKVersion,
)

AGENTIC_AGENT_NAME = "agentic"

AGENTS_DIR = Path(__file__).parent.parent.resolve() / "agents"

def create_agentic_scenario(sdk_version: SDKVersion) -> Scenario:
    return create_scenario(AGENTS_DIR, AGENTIC_AGENT_NAME, sdk_version, 5)

PYTHON_SCENARIO = create_agentic_scenario(SDKVersion.PYTHON)
DOTNET_SCENARIO = create_agentic_scenario(SDKVersion.DOTNET)
# NODEJS_SCENARIO = create_agentic_scenario(SDKVersion.NODEJS)
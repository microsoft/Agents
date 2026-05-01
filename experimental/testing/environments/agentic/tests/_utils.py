from pathlib import Path

from testing_common import (
    create_scenario,
    SDKVersion,
    SourceScenario,
)

AGENTIC_AGENT_NAME = "agentic"

AGENTS_DIR = Path(__file__).parent.parent.resolve() / "agents"

def create_agentic_scenario(sdk_version: SDKVersion) -> SourceScenario:
    return create_scenario(AGENTS_DIR, AGENTIC_AGENT_NAME, sdk_version)

PYTHON_SCENARIO = create_agentic_scenario(SDKVersion.PYTHON)
DOTNET_SCENARIO = create_agentic_scenario(SDKVersion.DOTNET)
# NODEJS_SCENARIO = create_agentic_scenario(SDKVersion.NODEJS)
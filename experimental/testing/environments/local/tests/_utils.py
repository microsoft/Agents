from pathlib import Path

from testing_common import (
    create_scenario,
    SDKVersion,
    SourceScenario,
)

AGENT_NAME = "quickstart"
AGENTS_DIR = Path(__file__).parent.parent.resolve() / "agents"

def _create_scenario(sdk_version: SDKVersion) -> SourceScenario:
    return create_scenario(AGENTS_DIR, AGENT_NAME, SDKVersion.PYTHON)

PYTHON_SCENARIO = _create_scenario(SDKVersion.PYTHON)
NET_SCENARIO    = _create_scenario(SDKVersion.NET)
JS_SCENARIO     = _create_scenario(SDKVersion.JS)
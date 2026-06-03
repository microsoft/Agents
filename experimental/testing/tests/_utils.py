from pathlib import Path

from microsoft_agents.testing import Scenario
from microsoft_agents.testing.cross_sdk import (
    create_scenario,
    SDKVersion,
)

CORE_AGENT_NAME = "core"
QUICKSTART_AGENT_BASE_NAME = "quickstart"
STREAM_AGENT_NAME = "stream"

AGENTS_DIR = Path(__file__).parent.parent.resolve() / "agents"

BLOB_STORAGE = "blob_storage"
COSMOS_DB = "cosmos_db"

def _create_core_scenario(sdk_version: SDKVersion) -> Scenario:
    return create_scenario(AGENTS_DIR, CORE_AGENT_NAME, sdk_version, delay=5.0)

def _create_quickstart_scenario_with_storage(sdk_version: SDKVersion, storage_type: str) -> Scenario:
    return create_scenario(
        AGENTS_DIR,
        f"{QUICKSTART_AGENT_BASE_NAME}_{storage_type}",
        sdk_version,
        delay=5.0,
    )

PYTHON_SCENARIO = _create_core_scenario(SDKVersion.PYTHON)
DOTNET_SCENARIO = _create_core_scenario(SDKVersion.DOTNET)
NODEJS_SCENARIO = _create_core_scenario(SDKVersion.NODEJS)

PYTHON_BLOB_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.PYTHON, BLOB_STORAGE)
DOTNET_BLOB_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.DOTNET, BLOB_STORAGE)
NODEJS_BLOB_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.NODEJS, BLOB_STORAGE)

PYTHON_COSMOS_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.PYTHON, COSMOS_DB)
DOTNET_COSMOS_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.DOTNET, COSMOS_DB)
NODEJS_COSMOS_SCENARIO = _create_quickstart_scenario_with_storage(SDKVersion.NODEJS, COSMOS_DB)
from pathlib import Path

from testing_common import (
    create_scenario,
    SDKVersion,
    SourceScenario,
)

AGENT_NAME = "quickstart"
AGENTS_DIR = Path(__file__).parent.parent.resolve() / "agents"

BLOB_STORAGE = "blob_storage"
COSMOS_DB = "cosmos_db"

def _create_scenario(sdk_version: SDKVersion) -> SourceScenario:
    return create_scenario(AGENTS_DIR, AGENT_NAME, sdk_version)

def _create_scenario_with_storage(sdk_version: SDKVersion, storage_type: str) -> SourceScenario:
    return create_scenario(
        AGENTS_DIR,
        f"{AGENT_NAME}_{storage_type}",
        sdk_version,
    )

PYTHON_SCENARIO = _create_scenario(SDKVersion.PYTHON)
NET_SCENARIO    = _create_scenario(SDKVersion.NET)
JS_SCENARIO     = _create_scenario(SDKVersion.JS)

PYTHON_BLOB_SCENARIO = _create_scenario_with_storage(SDKVersion.PYTHON, BLOB_STORAGE)
NET_BLOB_SCENARIO    = _create_scenario_with_storage(SDKVersion.NET, BLOB_STORAGE)
JS_BLOB_SCENARIO     = _create_scenario_with_storage(SDKVersion.JS, BLOB_STORAGE)

PYTHON_COSMOS_SCENARIO = _create_scenario_with_storage(SDKVersion.PYTHON, COSMOS_DB)
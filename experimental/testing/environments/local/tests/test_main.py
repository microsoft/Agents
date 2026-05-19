from microsoft_agents.testing import ExternalScenario

from ._utils import (
    PYTHON_SCENARIO,
    DOTNET_SCENARIO,
    NODEJS_SCENARIO,
    PYTHON_BLOB_SCENARIO,
    DOTNET_BLOB_SCENARIO,
    NODEJS_BLOB_SCENARIO,
    PYTHON_COSMOS_SCENARIO,
    DOTNET_COSMOS_SCENARIO,
    NODEJS_COSMOS_SCENARIO,
)

from .test_basic import BaseTestQuickstart
from .test_error import BaseTestError
from .test_expect_replies import BaseTestExpectReplies
from .test_streaming_response import BaseTestStreamingResponse

# Agents with Memory storage

class TestCorePython(
    BaseTestQuickstart,
    BaseTestError,
    BaseTestExpectReplies,
    BaseTestStreamingResponse,
):
    _scenario = PYTHON_SCENARIO

class TestCoreDotnet(
    BaseTestQuickstart,
    BaseTestError,
    BaseTestExpectReplies,
    BaseTestStreamingResponse,
):
    _scenario = DOTNET_SCENARIO
    _default_wait = 5.0

class TestCoreNodeJS(
    BaseTestQuickstart,
    BaseTestError,
    BaseTestExpectReplies,
    BaseTestStreamingResponse,
):
    _scenario = NODEJS_SCENARIO


# Agents with Blob Storage

class TestBlobPython(
    BaseTestQuickstart,
):
    _scenario = PYTHON_BLOB_SCENARIO

class TestBlobDotnet(
    BaseTestQuickstart,
):
    _scenario = DOTNET_BLOB_SCENARIO
    _default_wait = 5.0 

class TestBlobNodeJS(
    BaseTestQuickstart,
):
    _scenario = NODEJS_BLOB_SCENARIO


# Agents with CosmosDB Storage

class TestCosmosPython(
    BaseTestQuickstart,
):
    _scenario = PYTHON_COSMOS_SCENARIO

class TestCosmosDotnet(
    BaseTestQuickstart,
):
    _scenario = DOTNET_COSMOS_SCENARIO
    _default_wait = 5.0

class TestCosmosNodeJS(
    BaseTestQuickstart,
):
    _scenario = NODEJS_COSMOS_SCENARIO
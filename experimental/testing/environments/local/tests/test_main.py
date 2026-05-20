from microsoft_agents.testing import AgentClient
import pytest

from ._agent_client_mixin import AgentClientMixin
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
    # AgentClientMixin
):
    _scenario = PYTHON_SCENARIO

    @pytest.mark.asyncio
    async def test_language_command(self, agent_client: AgentClient):
        await agent_client.send("/language", wait=2.0)
        agent_client.expect().that_for_one(type="message", text="PYTHON")

class TestCoreDotnet(
    # BaseTestQuickstart,
    # BaseTestError,
    # BaseTestExpectReplies,
    # BaseTestStreamingResponse,
    AgentClientMixin
):
    _scenario = DOTNET_SCENARIO

    @pytest.mark.asyncio
    async def test_language_command(self, agent_client: AgentClient):
        await agent_client.send("/language", wait=5.0)
        agent_client.expect().that_for_one(type="message", text="DOTNET")

class TestCoreNodeJS(
    # BaseTestQuickstart,
    # BaseTestError,
    # BaseTestExpectReplies,
    # BaseTestStreamingResponse,
    AgentClientMixin
):
    _scenario = NODEJS_SCENARIO

    @pytest.mark.asyncio
    async def test_language_command(self, agent_client: AgentClient):
        await agent_client.send("/language", wait=5.0)
        agent_client.expect().that_for_one(type="message", text="NODEJS")

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
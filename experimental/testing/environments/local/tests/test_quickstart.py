import pytest

from microsoft_agents.testing import AgentClient

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

class BaseTestQuickstart:
    """Shared test logic for the Quickstart agent — language-agnostic."""

    @pytest.mark.asyncio
    async def test_conversation_update(self, agent_client: AgentClient):
        input_activity = agent_client.template.create({
            "type": "conversationUpdate",
            "members_added": [
                {"id": "bot-id", "name": "Bot"},
                {"id": "user1", "name": "User"},
            ],
            "textFormat": "plain",
            "entities": [{"type": "ClientCapabilities", "requiresBotState": True, "supportsTts": True}],
            "channel_data": {"clientActivityId": 123},
        })
        await agent_client.send(input_activity, wait=10)
        agent_client.expect().that_for_one(type="message", text="~Welcome")

    @pytest.mark.asyncio
    async def test_send_hello(self, agent_client: AgentClient):
        await agent_client.send("hello", wait=10)
        agent_client.expect().that_for_one(type="message", text="Hello!")

    @pytest.mark.asyncio
    async def test_send_hi(self, agent_client: AgentClient):
        await agent_client.send("hi", wait=10)
        responses = agent_client.recent()

        assert len(responses) == 2
        assert len(agent_client.history()) == 2
        agent_client.expect().that_for_one(type="message", text="you said: hi")
        agent_client.expect().that_for_one(type="typing")


# @pytest.mark.agent_test(PYTHON_SCENARIO)
# class TestQuickstartPython(BaseTestQuickstart):
#     pass

# @pytest.mark.agent_test(DOTNET_SCENARIO)
# class TestQuickstartDotNet(BaseTestQuickstart):
#     pass


# @pytest.mark.agent_test(NODEJS_SCENARIO)
# class TestQuickstartNodeJS(BaseTestQuickstart):
#     pass


# @pytest.mark.agent_test(PYTHON_BLOB_SCENARIO)
# class TestQuickstartPythonBlob(BaseTestQuickstart):
#     pass

# @pytest.mark.agent_test(DOTNET_BLOB_SCENARIO)
# class TestQuickstartDotNetBlob(BaseTestQuickstart):
#     pass

# @pytest.mark.agent_test(NODEJS_BLOB_SCENARIO)
# class TestQuickstartNodeJSBlob(BaseTestQuickstart):
#     pass


# @pytest.mark.agent_test(PYTHON_COSMOS_SCENARIO)
# class TestQuickstartPythonCosmos(BaseTestQuickstart):
#     pass

# @pytest.mark.agent_test(DOTNET_COSMOS_SCENARIO)
# class TestQuickstartDotNetCosmos(BaseTestQuickstart):
#     pass

@pytest.mark.agent_test(NODEJS_COSMOS_SCENARIO)
class TestQuickstartNodeJSCosmos(BaseTestQuickstart):
    pass

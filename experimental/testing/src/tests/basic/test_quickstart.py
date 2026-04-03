import pytest

from microsoft_agents.testing import AgentClient

from src.common import (
    create_scenario,
    SDKVersion,
)

AGENT_NAME = "quickstart"
PYTHON_SCENARIO = create_scenario(AGENT_NAME, SDKVersion.PYTHON)
NET_SCENARIO = create_scenario(AGENT_NAME, SDKVersion.NET)
JS_SCENARIO = create_scenario(AGENT_NAME, SDKVersion.JS)

class BaseTestQuickstart:
    """Integration tests for the Quickstart scenario."""

    @pytest.mark.asyncio
    async def test_conversation_update(self, agent_client: AgentClient):
        """Test sending a conversation update activity."""
        input_activity = agent_client.template.create({
            "type": "conversationUpdate",
            "members_added": [
                {"id": "bot-id", "name": "Bot"},
                {"id": "user1", "name": "User"},
            ],
            "textFormat": "plain",
            "entities": [
                {
                    "type": "ClientCapabilities",
                    "requiresBotState": True,
                    "supportsTts": True
                }
            ],
            "channel_data": {"clientActivityId": 123}
        })

        await agent_client.send(input_activity, wait=10)
        agent_client.expect().that_for_one(type="message", text="~Welcome")

    @pytest.mark.asyncio
    async def test_send_hello(self, agent_client: AgentClient):
        """Test sending a 'hello' message and receiving a response."""
        await agent_client.send("hello", wait=10)
        agent_client.expect().that_for_one(type="message", text="Hello!")

    @pytest.mark.asyncio
    async def test_send_hi(self, agent_client: AgentClient):
        """Test sending a 'hi' message and receiving a response."""
    
        await agent_client.send("hi", wait=10)
        responses = agent_client.recent()

        assert len(responses) == 2
        assert len(agent_client.history()) == 2

        agent_client.expect().that_for_one(type="message", text="you said: hi")
        agent_client.expect().that_for_one(type="typing")


@pytest.mark.agent_test(PYTHON_SCENARIO)
class TestQuickstartPython(BaseTestQuickstart):
    pass

@pytest.mark.agent_test(NET_SCENARIO)
class TestQuickstartNet(BaseTestQuickstart):
    pass

@pytest.mark.agent_test(JS_SCENARIO)
class TestQuickstartJS(BaseTestQuickstart):
    pass
import pytest

from microsoft_agents.activity import Activity
from microsoft_agents.testing import AgentClient

from ._utils import (
    PYTHON_AGENTIC_SCENARIO
)

class BaseTestAgentic:

    @pytest.mark.asyncio
    async def test_echo(self, agent_client: AgentClient):
        await agent_client.send("hi", wait=10)
        responses = agent_client.recent()

        assert len(responses) == 2
        assert len(agent_client.history()) == 2
        agent_client.expect().that_for_one(type="message", text="You said: hi")
        agent_client.expect().that_for_one(type="typing")

    @pytest.mark.asyncio
    async def test_auth_flow(self, agent_client: AgentClient):

        await agent_client.send("/me", wait=5)
        responses = agent_client.recent()

        assert len(responses) == 2
        assert len(agent_client.history()) == 2
        agent_client.expect().that_for_one(type="message", text="You are authenticated as:")
        agent_client.expect().that_for_one(type="typing")

@pytest.mark.agent_test(PYTHON_AGENTIC_SCENARIO)
class TestAgenticPython(BaseTestAgentic):
    pass
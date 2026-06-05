import pytest

from microsoft_agents.testing import AgentClient

from ._agent_client_mixin import AgentClientMixin
from ._utils import wait_for_activity

class BaseTestQuickstart(AgentClientMixin):
    """Shared test logic for the Quickstart agent — language-agnostic."""

    @pytest.mark.asyncio
    async def test_conversation_update(self, agent_client: AgentClient):
        input_activity = agent_client.template.create({
            "type": "conversationUpdate",
            "members_added": [
                {"id": "bot-id", "name": "Bot"},
                {"id": "user1", "name": "User"},
            ],
            "channel_data": {"clientActivityId": 123},
        })
        await agent_client.send(input_activity)
        await wait_for_activity(agent_client, "message", timeout=10.0)
        agent_client.expect().that_for_one(type="message", text="~Welcome")

    @pytest.mark.asyncio
    async def test_send_hello(self, agent_client: AgentClient):
        await agent_client.send("hello")
        await wait_for_activity(agent_client, "message", timeout=10.0)
        agent_client.expect().that_for_one(type="message", text="You said: hello")

    @pytest.mark.asyncio
    async def test_send_hi(self, agent_client: AgentClient):
        await agent_client.send("hi")
        await wait_for_activity(agent_client, "message", timeout=10.0)
        agent_client.expect().that_for_one(type="message", text="You said: hi")


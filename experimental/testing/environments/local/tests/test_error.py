import pytest

from microsoft_agents.testing import AgentClient

from ._agent_client_mixin import AgentClientMixin

class BaseTestError(AgentClientMixin):

    @pytest.mark.asyncio
    async def test_error(self, agent_client: AgentClient):
        """Test sending an activity with expectReplies delivery mode without a service URL."""

        await agent_client.send("/error", wait=2.0)

        agent_client.expect().that_for_one(
            type="message",
            text="The bot encountered an error or bug."
        )
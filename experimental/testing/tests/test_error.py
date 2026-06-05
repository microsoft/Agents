import pytest

from microsoft_agents.testing import AgentClient

from ._agent_client_mixin import AgentClientMixin
from ._utils import wait_for_activity

class BaseTestError(AgentClientMixin):

    @pytest.mark.asyncio
    async def test_error(self, agent_client: AgentClient):
        """Test sending an activity with expectReplies delivery mode without a service URL."""

        # with pytest.raises(ClientError):
        await agent_client.send("/error")
        await wait_for_activity(agent_client, "message", timeout=10.0)

        agent_client.expect().that_for_one(
            type="message",
            text="The bot encountered an error or bug."
        )
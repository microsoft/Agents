import pytest

from microsoft_agents.activity import Activity
from microsoft_agents.testing import AgentClient

from ._agent_client_mixin import AgentClientMixin

class BaseTestExpectReplies(AgentClientMixin):

    @pytest.mark.asyncio
    async def test_expect_replies(self, agent_client: AgentClient):
        """Test sending an activity with expectReplies delivery mode without a service URL."""

        activity = Activity.model_validate(dict(
            type="message",
            text="hi",
            conversation={"id": "conv-id"},
            channel_id="test",
            from_property={"id": "from-id"},
            recipient={"id": "to-id"},
            delivery_mode="expectReplies",
            locale="en-US",
        ))

        res = await agent_client.send_expect_replies(activity)

        assert len(res) > 0
        assert isinstance(res[0], Activity)
        assert res[0].text == "You said: hi"
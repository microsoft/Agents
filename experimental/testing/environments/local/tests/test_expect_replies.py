import pytest

from microsoft_agents.activity import Activity
from microsoft_agents.testing import AgentClient

from ._utils import (
    PYTHON_SCENARIO,
    DOTNET_SCENARIO,
    NODEJS_SCENARIO,
)

class BaseTestExpectReplies:
    async def test_expect_replies_without_service_url(self, agent_client: AgentClient):
        """Test sending an activity with expectReplies delivery mode without a service URL."""

        activity = Activity(
            type="message",
            text="hi",
            conversation={"id": "conv-id"},
            channel_id="test",
            from_property={"id": "from-id"},
            recipient={"id": "to-id"},
            delivery_mode="expectReplies",
            locale="en-US",
        )

        res = await agent_client.send_expect_replies(activity)

        assert len(res) > 0
        assert isinstance(res[0], Activity)

@pytest.mark.agent_test(PYTHON_SCENARIO)
class TestExpectRepliesPython(BaseTestExpectReplies):
    pass

@pytest.mark.agent_test(DOTNET_SCENARIO)
class TestExpectRepliesDotNet(BaseTestExpectReplies):
    pass


@pytest.mark.agent_test(NODEJS_SCENARIO)
class TestExpectRepliesNodeJS(BaseTestExpectReplies):
    pass

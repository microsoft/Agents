import pytest

from microsoft_agents.activity import Activity
from microsoft_agents.testing import AgentClient

from ._utils import (
    PYTHON_SCENARIO,
    DOTNET_SCENARIO,
    NODEJS_SCENARIO,
)

class BaseTestError:

    @pytest.mark.asyncio
    async def test_error(self, agent_client: AgentClient):
        """Test sending an activity with expectReplies delivery mode without a service URL."""

        await agent_client.send("/error", wait=2.0)

        agent_client.expect().that_for_one(
            type="message",
            text="The bot encountered an error or bug."
        )

@pytest.mark.agent_test(PYTHON_SCENARIO)
class TestErrorPython(BaseTestError):
    pass

@pytest.mark.agent_test(DOTNET_SCENARIO)
class TestErrorDotNet(BaseTestError):
    pass

@pytest.mark.agent_test(NODEJS_SCENARIO)
class TestErrorNodeJS(BaseTestError):
    pass

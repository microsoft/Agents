import pytest

from microsoft_agents.testing import Scenario

class AgentClientMixin:

    _scenario: Scenario

    @pytest.fixture(scope="class")
    async def agent_client(self):
        """Fixture to provide an AgentClient instance for the tests."""
        async with self._scenario.client() as client:
            yield client
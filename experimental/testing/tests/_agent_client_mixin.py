import pytest

from microsoft_agents.testing import (
    AgentClient,
    Scenario,
    JsonTranscriptFormatter,
)

class AgentClientMixin:

    _scenario: Scenario

    @pytest.fixture(scope="class")
    async def _agent_client(self):
        """Fixture to provide an AgentClient instance for the tests."""
        async with self._scenario.client() as client:
            yield client

    @pytest.fixture(scope="function")
    def agent_client(self, _agent_client: AgentClient, request: pytest.FixtureRequest):
        """Fixture to reset the AgentClient state before each test."""
        _agent_client.clear()
        yield _agent_client

        if request.session.testsfailed:
            # If the test failed, print the transcript for debugging
            formatter = JsonTranscriptFormatter(
                model_dump_args={
                    "exclude_none": True
                }
            )
            print(f"\n\n{formatter.format(_agent_client.transcript)}")
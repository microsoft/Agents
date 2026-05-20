import pytest

import os

from microsoft_agents.activity import (
    Activity,
    RoleTypes,
)
from microsoft_agents.testing import AgentClient, ExternalScenario

from dotenv import dotenv_values

ENVIRONMENT = dotenv_values(".env")

from ._agent_client_mixin import AgentClientMixin
from ._utils import (
    PYTHON_SCENARIO,
    DOTNET_SCENARIO,
)

class BaseTestAgentic(AgentClientMixin):

    # @pytest.mark.asyncio
    # async def test_echo(self, agent_client: AgentClient):
    #     await agent_client.send("hi", wait=10)
    #     responses = agent_client.recent()

    #     assert len(responses) == 2
    #     assert len(agent_client.history()) == 2
    #     agent_client.expect().that_for_one(type="message", text="You said: hi")
    #     agent_client.expect().that_for_one(type="typing")

    @pytest.mark.asyncio
    async def test_auth_flow(self, agent_client: AgentClient):

        activity = Activity(
            type="message",
            text="/agentic",
            from_property=dict(
                id="user"
            ),
            recipient=dict(
                role=RoleTypes.agentic_user,
                agentic_app_id=ENVIRONMENT.get("AGENT_INSTANCE_ID"),
                agentic_user_id=ENVIRONMENT.get("AGENT_USER_ID"),
                upn=ENVIRONMENT.get("AGENT_UPN"),
                tenant_id=ENVIRONMENT.get("TENANT_ID")
            )
        )

        await agent_client.send(activity, wait=5)
        responses = agent_client.recent()

        assert len(responses) == 2
        assert len(agent_client.history()) == 2
        agent_client.expect().that_for_one(type="message", text="~Acquired agentic user token with length:")

# class TestAgenticPython(BaseTestAgentic):
#     _scenario = PYTHON_SCENARIO

# @pytest.mark.agent_test(DOTNET_SCENARIO)
# class TestAgenticDotNet(BaseTestAgentic):
#     pass

EXTERNAL_SCENARIO = ExternalScenario("http://localhost:3978/api/messages")

class TestStreamingResponseExternalScenario(BaseTestAgentic):
    _scenario = EXTERNAL_SCENARIO
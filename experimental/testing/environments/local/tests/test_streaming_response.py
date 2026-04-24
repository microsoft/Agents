import pytest

from microsoft_agents.activity import (
    Activity,
    ActivityTypes,
    Channels,
    Entity
)
from microsoft_agents.testing import AgentClient

from ._utils import (
    PYTHON_STREAM_SCENARIO,
    # DOTNET_STREAM_SCENARIO,
    # NODEJS_STREAM_SCENARIO,
)

FULL_TEXT = "This is a streaming response."
CHUNKS = FULL_TEXT.split()

def get_streaminfo(activity: Activity) -> Entity:
    for entity in activity.entities:
        if isinstance(entity, dict) and entity.get("type") == "streaminfo":
            return Entity.model_validate(entity)
        elif isinstance(entity, Entity) and entity.type == "streaminfo":
            return entity
    raise ValueError("No streaminfo entity found")

class BaseTestStreamingResponse:
    
    @pytest.mark.asyncio
    async def test_basic_streaming_response_streaming_channel(self, agent_client: AgentClient):

        expected_len = len(FULL_TEXT.split())

        agent_client.template = agent_client.template.with_updates(channel_id=Channels.webchat)

        # give enough time for all the activities to send
        await agent_client.send("/stream", wait=expected_len * 2.0)

        stream_activities = agent_client.select().where(
            entities=lambda x: any(e["type"] == "streaminfo" for e in x)
        ).get()

        assert len(stream_activities) == len(CHUNKS) + 1

        informative = stream_activities[0]
        informative_streaminfo = get_streaminfo(informative)

        assert informative_streaminfo.stream_type == "informative"
        assert informative_streaminfo.stream_sequence == 1
        assert informative.text == "Starting stream..."
        assert informative.type == ActivityTypes.typing

        t = ""
        for i, chunk in enumerate(CHUNKS[:-1]):
            t += chunk

            j = i + 1

            streaminfo = get_streaminfo(stream_activities[j])

            assert stream_activities[j].text == t
            assert stream_activities[j].type == ActivityTypes.typing
            assert streaminfo.stream_type == "streaming"
            assert streaminfo.stream_sequence == j + 1

        final_streaminfo = get_streaminfo(stream_activities[-1])

        assert final_streaminfo.stream_sequence == len(stream_activities)
        assert final_streaminfo.stream_type == "final"
        assert stream_activities[-1].text == FULL_TEXT.replace(" ", "")

@pytest.mark.agent_test(PYTHON_STREAM_SCENARIO)
class TestStreamingResponsePython(BaseTestStreamingResponse):
    pass

# @pytest.mark.agent_test(DOTNET_STREAM_SCENARIO)
# class TestStreamingResponseDotNet(BaseTestStreamingResponse):
#     pass

# @pytest.mark.agent_test(NODEJS_STREAM_SCENARIO)
# class TestStreamingResponseNodeJS(BaseTestStreamingResponse):
#     pass
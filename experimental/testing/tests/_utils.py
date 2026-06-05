from microsoft_agents.testing import AgentClient
from microsoft_agents.testing.utils import poll

async def wait_for_activity(agent_client: AgentClient, activity_type: str | None = "", timeout: float = 1.0):
    condition = {}
    if activity_type is not None:
        condition["type"] = activity_type

    return await poll(
        lambda: agent_client.select().where(condition).count() > 0,
        timeout=timeout
    )
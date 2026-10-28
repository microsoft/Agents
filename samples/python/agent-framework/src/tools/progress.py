# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from contextvars import ContextVar

from microsoft_agents.activity import Channels
from microsoft_agents.hosting.core import TurnContext

_turn_context: ContextVar[TurnContext | None] = ContextVar(
    "weather_tool_turn_context", default=None
)


def set_turn_context(context: TurnContext):
    return _turn_context.set(context)


def reset_turn_context(token) -> None:
    _turn_context.reset(token)


async def report_progress(message: str) -> None:
    """Send tool progress through the active Agents SDK turn, when available."""
    print(message)
    context = _turn_context.get()
    if context is None:
        return

    if context.activity.channel_id == Channels.webchat:
        await context.send_activity(message)
    else:
        context.streaming_response.queue_informative_update(message)

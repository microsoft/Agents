# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from contextvars import ContextVar, Token

from microsoft_agents.hosting.core import TurnContext

_turn_context: ContextVar[TurnContext | None] = ContextVar(
    "weather_tool_turn_context", default=None
)


def set_turn_context(context: TurnContext) -> Token:
    return _turn_context.set(context)


def reset_turn_context(token: Token) -> None:
    _turn_context.reset(token)


async def report_progress(message: str) -> None:
    print(message)
    context = _turn_context.get()
    if context is not None:
        context.streaming_response.queue_informative_update(message)

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import logging
import time
from dotenv import load_dotenv

from os import environ
from microsoft_agents.hosting.aiohttp import CloudAdapter
from microsoft_agents.hosting.core import (
    Authorization,
    AgentApplication,
    TurnState,
    TurnContext,
    MemoryStorage,
    RouteRank,
)
from microsoft_agents.authentication.msal import MsalConnectionManager
from microsoft_agents.activity import load_configuration_from_env
from opentelemetry.trace import Status, StatusCode

from .agent_telemetry import get_tracer, record_message_duration, record_route_execution

load_dotenv()
agents_sdk_config = load_configuration_from_env(environ)

STORAGE = MemoryStorage()
CONNECTION_MANAGER = MsalConnectionManager(**agents_sdk_config)
ADAPTER = CloudAdapter(connection_manager=CONNECTION_MANAGER)
AUTHORIZATION = Authorization(STORAGE, CONNECTION_MANAGER, **agents_sdk_config)
LOGGER = logging.getLogger(__name__)


AGENT_APP = AgentApplication[TurnState](
    storage=STORAGE, adapter=ADAPTER, authorization=AUTHORIZATION, **agents_sdk_config
)


@AGENT_APP.conversation_update("membersAdded")
async def on_members_added(context: TurnContext, _state: TurnState):
    tracer = get_tracer()

    with tracer.start_as_current_span("agent.welcome_message") as span:
        conversation_id = context.activity.conversation.id if context.activity.conversation else "unknown"
        channel_id = context.activity.channel_id or "unknown"

        span.set_attribute("conversation.id", conversation_id)
        span.set_attribute("channel.id", channel_id)
        span.set_attribute("members.added.count", len(context.activity.members_added or []))

        try:
            for member in context.activity.members_added or []:
                if member.id != context.activity.recipient.id:
                    span.add_event(
                        "member.added",
                        {
                            "member.id": member.id,
                            "member.name": member.name or "unknown",
                        },
                        timestamp=time.time_ns(),
                    )

            await context.send_activity("Hello and Welcome!")
            record_route_execution("welcome_message", conversation_id)
            LOGGER.info("Welcome message sent for conversation %s", conversation_id)
            span.set_status(Status(StatusCode.OK))
        except Exception as error:
            span.record_exception(error)
            span.set_status(Status(StatusCode.ERROR, str(error)))
            LOGGER.exception("Welcome message failed for conversation %s", conversation_id)
            raise

    return True


@AGENT_APP.activity("message", rank=RouteRank.LAST)
async def on_message(context: TurnContext, _state: TurnState):
    tracer = get_tracer()
    t0 = time.perf_counter()
    conversation_id = context.activity.conversation.id if context.activity.conversation else "unknown"
    channel_id = context.activity.channel_id or "unknown"
    status = "success"

    with tracer.start_as_current_span("agent.message_handler") as span:
        span.set_attribute("conversation.id", conversation_id)
        span.set_attribute("channel.id", channel_id)
        span.set_attribute("message.text.length", len(context.activity.text or ""))
        span.set_attribute("user.id", context.activity.from_property.id if context.activity.from_property else "unknown")
        span.add_event(
            "message.received",
            {
                "message.id": context.activity.id or "unknown",
                "message.text": context.activity.text or "",
                "user.id": context.activity.from_property.id if context.activity.from_property else "unknown",
                "channel.id": channel_id,
            },
            timestamp=time.time_ns(),
        )

        try:
            await context.send_activity(f"You said: {context.activity.text}")
            span.add_event("response.sent", timestamp=time.time_ns())
            record_route_execution("message_handler", conversation_id)
            LOGGER.info("Message handled for conversation %s", conversation_id)
            span.set_status(Status(StatusCode.OK))
            status = "success"
        except Exception as error:
            span.record_exception(error)
            span.set_status(Status(StatusCode.ERROR, str(error)))
            LOGGER.exception("Message handling failed for conversation %s", conversation_id)
            status = "error"
            raise
        finally:
            elapsed_ms = (time.perf_counter() - t0) * 1000
            record_message_duration(elapsed_ms, conversation_id, channel_id, status)

@AGENT_APP.error
async def on_error(context: TurnContext, error: Exception):
    LOGGER.exception("Unhandled error in conversation %s, %s", context.activity.conversation.id if context.activity.conversation else "unknown", str(error))

    # Send a message to the user
    await context.send_activity("The bot encountered an error or bug.")

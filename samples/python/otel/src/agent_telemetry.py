# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from opentelemetry import trace
from opentelemetry import metrics

SERVICE_NAME = "OTelAgent"
SERVICE_VERSION = "1.0.0"
TRACER = None
ROUTE_EXECUTED_COUNTER = None
MESSAGE_PROCESSING_DURATION = None


def set_telemetry_globals(service_name, tracer, route_executed_counter, message_processing_duration):
    """Set the telemetry globals after OTel providers are configured."""
    global SERVICE_NAME, TRACER, ROUTE_EXECUTED_COUNTER, MESSAGE_PROCESSING_DURATION
    SERVICE_NAME = service_name
    TRACER = tracer
    ROUTE_EXECUTED_COUNTER = route_executed_counter
    MESSAGE_PROCESSING_DURATION = message_processing_duration


def get_tracer():
    return TRACER or trace.get_tracer(SERVICE_NAME, SERVICE_VERSION)

def record_route_execution(route_type: str, conversation_id: str):
    if ROUTE_EXECUTED_COUNTER is not None:
        ROUTE_EXECUTED_COUNTER.add(
            1,
            {
                "route.type": route_type,
                "conversation.id": conversation_id,
            },
        )


def record_message_duration(duration_ms: float, conversation_id: str, channel_id: str, status: str):
    if MESSAGE_PROCESSING_DURATION is not None:
        MESSAGE_PROCESSING_DURATION.record(
            duration_ms,
            {
                "conversation.id": conversation_id,
                "channel.id": channel_id,
                "status": status,
            },
        )

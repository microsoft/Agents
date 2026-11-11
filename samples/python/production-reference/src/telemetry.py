# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import asyncio

from opentelemetry import metrics, trace
from opentelemetry.trace import Status, StatusCode
from opentelemetry.sdk.trace import TracerProvider, SpanProcessor
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.metrics.view import View, DropAggregation
from opentelemetry.sdk.resources import Resource
from os import environ


class SampleSpanProcessor(SpanProcessor):
    def __init__(self, exporter):
        self.processor = BatchSpanProcessor(exporter)

    def on_end(self, span):
        if span.instrumentation_scope.name == "agents-production-reference":
            self.processor.on_end(span)

    def shutdown(self):
        self.processor.shutdown()

    def force_flush(self, timeout_millis=30000):
        return self.processor.force_flush(timeout_millis)


class Telemetry:
    def __init__(self, connection_string: str = ""):
        if connection_string:
            from azure.monitor.opentelemetry.exporter import (
                AzureMonitorTraceExporter,
                AzureMonitorMetricExporter,
            )

            # Export only our deliberately content-free instrumentation. SDK spans
            # can include conversation identifiers; HTTP auto-instrumentation can
            # include destinations and exception messages.
            resource = Resource.create(
                {
                    "service.name": environ.get(
                        "OTEL_SERVICE_NAME", "agents-sdk-production-reference-python"
                    )
                }
            )
            provider = TracerProvider(resource=resource)
            provider.add_span_processor(
                SampleSpanProcessor(
                    AzureMonitorTraceExporter(connection_string=connection_string),
                )
            )
            trace.set_tracer_provider(provider)
            metrics.set_meter_provider(
                MeterProvider(
                    resource=resource,
                    metric_readers=[
                        PeriodicExportingMetricReader(
                            AzureMonitorMetricExporter(connection_string=connection_string),
                        )
                    ],
                    views=[
                        View(instrument_name="*", aggregation=DropAggregation()),
                        View(meter_name="agents-production-reference", instrument_name="agent.*"),
                    ],
                )
            )
        self.tracer = trace.get_tracer("agents-production-reference")
        meter = metrics.get_meter("agents-production-reference")
        self.turns = meter.create_counter("agent.turns.total")
        self.failures = meter.create_counter("agent.failures.total")
        self.requests = meter.create_counter("agent.http.requests.total")
        self.latency = meter.create_histogram("agent.http.duration", unit="s")

    def request(self, status: int, duration: float):
        attributes = {"status": status}
        self.requests.add(1, attributes)
        self.latency.record(duration, attributes)
        if status in {401, 403}:
            self.failure("authentication")

    def failure(self, category: str):
        self.failures.add(1, {"category": category})

    async def shutdown(self):
        for provider in (trace.get_tracer_provider(), metrics.get_meter_provider()):
            shutdown = getattr(provider, "shutdown", None)
            if shutdown:
                await asyncio.to_thread(shutdown)

    def failed_span(self, span):
        span.set_status(Status(StatusCode.ERROR))

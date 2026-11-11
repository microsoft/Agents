# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export.in_memory_span_exporter import InMemorySpanExporter

from src.telemetry import SampleSpanProcessor


def test_unreviewed_sdk_content_is_not_exported():
    exporter = InMemorySpanExporter()
    provider = TracerProvider()
    provider.add_span_processor(SampleSpanProcessor(exporter))
    sdk = provider.get_tracer("unreviewed-sdk-source")
    application = provider.get_tracer("agents-production-reference")
    with sdk.start_as_current_span("sdk.turn") as span:
        span.set_attribute("user.message", "synthetic private content")
        with application.start_as_current_span(
            "agent.support_issue_capture.turn", record_exception=False
        ):
            pass
    provider.force_flush()
    spans = exporter.get_finished_spans()
    assert len(spans) == 1
    assert spans[0].name == "agent.support_issue_capture.turn"
    assert not spans[0].attributes
    assert not spans[0].events
    provider.shutdown()

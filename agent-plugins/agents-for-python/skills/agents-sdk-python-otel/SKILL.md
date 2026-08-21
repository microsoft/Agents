---
name: agents-sdk-python-otel
description: >
  Use when adding, configuring, validating, or troubleshooting OpenTelemetry
  observability for a Microsoft 365 Agents SDK application in Python. Trigger
  when the user mentions OpenTelemetry, OTel, telemetry, traces, metrics,
  logs, OTLP, Aspire Dashboard, Application Insights, Azure Monitor,
  distributed tracing, custom tracers or meters, span sampling, or observing
  microsoft_agents SDK signals.
---

# OpenTelemetry for Agents SDK (Python)

## Goal

Configure traces, metrics, and logs for a Python Agents SDK application,
including the SDK's built-in telemetry and a local Aspire Dashboard. Preserve
the project's dependency manager, web host, logging design, and startup
lifecycle.

## Non-negotiable rules

- Configure global OpenTelemetry providers before importing most
  `microsoft_agents` SDK modules. The Python SDK resolves tracers and meters at
  import time. `microsoft_agents.activity` contains no telemetry and is safe
  to import before provider setup.
- Extend existing providers instead of attempting to replace already-set
  global providers. OpenTelemetry Python does not permit provider replacement
  after initialization.
- Instrument aiohttp, requests, or other libraries after providers are
  configured and before the application starts handling traffic.
- Use one stable application-specific `service.name`.
- Let exporters read standard OTEL environment variables. Never hardcode
  collector credentials, API keys, or connection strings.
- Never add prompts, responses, message text, tokens, authorization headers,
  cookies, secrets, attachment contents, user names, user IDs, conversation
  IDs, activity IDs, or arbitrary HTTP headers to custom telemetry by default.
- Keep custom metric attributes low-cardinality. Good dimensions include
  route type, activity type, channel ID, operation, status, and error type.
- Use batch processors for normal applications. Simple processors are only
  appropriate for narrow debugging or tests.
- Do not set always-on sampling as a production default. Honor the deployment
  sampling policy or configure a deliberate parent-based ratio.
- Flush tracer, meter, and logger providers during graceful shutdown. Do not
  silently ignore exporter or shutdown failures.
- Avoid duplicate `LoggingHandler` instances on the root logger.
- Aspire Dashboard anonymous mode is local-development only. Bind it to
  loopback and never deploy that configuration.
- Telemetry must record and re-raise application exceptions.

## Workflow

Complete these steps in order.

### 1. Inspect the application

Find:

- `pyproject.toml`, `requirements*.txt`, lockfiles, Python version, virtual
  environment convention, and dependency manager.
- The actual startup module and import order.
- Whether the app uses aiohttp, FastAPI, Flask, requests, httpx, or another
  server/client stack.
- Existing global tracer, meter, or logger providers.
- Existing OTLP, Azure Monitor, Application Insights, or vendor-specific
  exporters.
- Existing logging handlers and shutdown hooks.
- Existing custom tracers, meters, instruments, and instrumentation calls.
- OTEL settings in `.env` templates, deployment manifests, containers, and
  CI/CD configuration.

If any provider is already configured, merge the missing exporter,
instrumentation, processor, or logging behavior into that setup. Do not call
`trace.set_tracer_provider`, `metrics.set_meter_provider`, or
`set_logger_provider` a second time.

### 2. Add only the required packages

Use the project's dependency manager and preserve its lockfile. For pip and an
aiohttp application:

```powershell
python -m pip install `
  opentelemetry-api `
  opentelemetry-sdk `
  opentelemetry-exporter-otlp `
  opentelemetry-instrumentation-aiohttp-server `
  opentelemetry-instrumentation-aiohttp-client `
  opentelemetry-instrumentation-requests
```

Install only instrumentations for libraries the application actually uses.
Examples:

- `opentelemetry-instrumentation-fastapi`
- `opentelemetry-instrumentation-flask`
- `opentelemetry-instrumentation-httpx`
- `opentelemetry-instrumentation-logging`

Keep OpenTelemetry API, SDK, exporter, and instrumentation versions compatible.
Do not add a second dependency manifest or switch package managers.

### 3. Configure providers in an SDK-free bootstrap module

Create `instrumentation.py` without importing any Agents SDK telemetry module:

```python
from __future__ import annotations

import logging
import os
import socket
from dataclasses import dataclass

from opentelemetry import metrics, trace
from opentelemetry._logs import set_logger_provider
from opentelemetry.exporter.otlp.proto.grpc._log_exporter import (
    OTLPLogExporter,
)
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import (
    OTLPMetricExporter,
)
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import (
    OTLPSpanExporter,
)
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor


@dataclass(frozen=True)
class TelemetryProviders:
    tracer_provider: TracerProvider
    meter_provider: MeterProvider
    logger_provider: LoggerProvider

    def shutdown(self) -> None:
        self.logger_provider.shutdown()
        self.meter_provider.shutdown()
        self.tracer_provider.shutdown()


def configure_otel_providers(
    service_name: str,
    service_version: str | None = None,
) -> TelemetryProviders:
    resolved_service_name = os.getenv("OTEL_SERVICE_NAME", service_name)
    resource_attributes: dict[str, str] = {
        "service.name": resolved_service_name,
        "service.instance.id": socket.gethostname(),
        "telemetry.sdk.language": "python",
    }

    if service_version:
        resource_attributes["service.version"] = service_version

    resource = Resource.create(resource_attributes)

    tracer_provider = TracerProvider(resource=resource)
    tracer_provider.add_span_processor(
        BatchSpanProcessor(OTLPSpanExporter())
    )
    trace.set_tracer_provider(tracer_provider)

    meter_provider = MeterProvider(
        resource=resource,
        metric_readers=[
            PeriodicExportingMetricReader(OTLPMetricExporter())
        ],
    )
    metrics.set_meter_provider(meter_provider)

    logger_provider = LoggerProvider(resource=resource)
    logger_provider.add_log_record_processor(
        BatchLogRecordProcessor(OTLPLogExporter())
    )
    set_logger_provider(logger_provider)

    root_logger = logging.getLogger()
    if not any(
        isinstance(handler, LoggingHandler)
        for handler in root_logger.handlers
    ):
        root_logger.addHandler(
            LoggingHandler(
                level=logging.NOTSET,
                logger_provider=logger_provider,
            )
        )

    return TelemetryProviders(
        tracer_provider=tracer_provider,
        meter_provider=meter_provider,
        logger_provider=logger_provider,
    )
```

Exporter constructors intentionally omit endpoints and headers so they honor
the standard OTEL environment variables.

If the application already configured a provider, do not use this complete
template. Modify the existing provider setup instead.

### 4. Enforce provider-before-SDK import order

The startup module must load environment variables, configure providers, and
only then import the agent:

```python
from dotenv import load_dotenv

load_dotenv()

from .instrumentation import configure_otel_providers

TELEMETRY = configure_otel_providers(
    service_name="contoso-support-agent",
    service_version="1.0.0",
)

# These imports must remain below provider initialization.
from .agent import AGENT_APP, CONNECTION_MANAGER
from .start_server import start_server

start_server(
    agent_application=AGENT_APP,
    auth_configuration=(
        CONNECTION_MANAGER.get_default_connection_configuration()
    ),
    telemetry=TELEMETRY,
)
```

Do not move `microsoft_agents.hosting.core`,
`microsoft_agents.hosting.aiohttp`, authentication, storage, connector, or
agent-module imports above provider initialization.

### 5. Instrument the libraries the app uses

For aiohttp server/client and requests:

```python
from opentelemetry.instrumentation.aiohttp_client import (
    AioHttpClientInstrumentor,
)
from opentelemetry.instrumentation.aiohttp_server import (
    AioHttpServerInstrumentor,
)
from opentelemetry.instrumentation.requests import RequestsInstrumentor


def instrument_libraries() -> None:
    AioHttpServerInstrumentor().instrument()
    AioHttpClientInstrumentor().instrument()
    RequestsInstrumentor().instrument()
```

Call `instrument_libraries()` after provider setup and before creating the web
application or making instrumented requests.

Do not enrich spans with complete URLs, query strings, headers, request
bodies, response bodies, prompts, or agent messages. If custom hooks are
required, add only reviewed, bounded attributes.

### 6. Integrate graceful shutdown

For aiohttp, attach cleanup to the application rather than relying only on
process exit:

```python
import asyncio
from aiohttp import web

from .instrumentation import TelemetryProviders


def create_app(
    telemetry: TelemetryProviders,
) -> web.Application:
    app = web.Application()

    async def shutdown_telemetry(_: web.Application) -> None:
        await asyncio.to_thread(telemetry.shutdown)

    app.on_cleanup.append(shutdown_telemetry)
    return app
```

For another host, use its supported lifespan or shutdown callback. For a
simple synchronous process:

```python
try:
    run_application()
finally:
    TELEMETRY.shutdown()
```

Register one owner for shutdown. Do not call provider shutdown both from the
web host and an `atexit` handler.

### 7. Configure OTLP

For the local Aspire Dashboard using OTLP/gRPC, add non-secret defaults to the
uncommitted `.env` and document them in `env.TEMPLATE` or `.env.example`:

```dotenv
OTEL_SERVICE_NAME=contoso-support-agent
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

Use the standard names above. Do not use nested application-configuration
forms such as `OTEL__EXPORTER__OTLP__ENDPOINT`; OpenTelemetry exporters read
`OTEL_EXPORTER_OTLP_ENDPOINT`.

Standard variables to support:

| Variable | Purpose |
|---|---|
| `OTEL_SERVICE_NAME` | Stable application service name |
| `OTEL_RESOURCE_ATTRIBUTES` | Stable resource metadata |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Base OTLP collector endpoint |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Usually `grpc` or `http/protobuf` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Collector authentication headers; keep secret |
| `OTEL_TRACES_SAMPLER` | Trace sampling strategy |
| `OTEL_TRACES_SAMPLER_ARG` | Sampling strategy argument |
| `OTEL_METRIC_EXPORT_INTERVAL` | Metric export interval in milliseconds |

Use deployment secrets for OTLP headers and Azure Monitor connection strings.
Never commit populated `.env` files.

### 8. Add custom business telemetry only when useful

The Agents SDK already emits adapter, application, turn, connector, storage,
authentication, authorization, and user-token-client signals. Add custom
telemetry only for application-specific operations.

Import this helper only after providers are configured:

```python
from opentelemetry import metrics, trace

SOURCE_NAME = "Contoso.SupportAgent"
SOURCE_VERSION = "1.0.0"

TRACER = trace.get_tracer(SOURCE_NAME, SOURCE_VERSION)
METER = metrics.get_meter(SOURCE_NAME, SOURCE_VERSION)

ROUTE_EXECUTIONS = METER.create_counter(
    "agent.route.executions",
    unit="{execution}",
    description="Number of application route executions.",
)
ROUTE_DURATION = METER.create_histogram(
    "agent.route.duration",
    unit="ms",
    description="Application route execution duration.",
)
```

Instrument a route with bounded attributes:

```python
import time

from opentelemetry.trace import Status, StatusCode

started = time.perf_counter()
status = "ok"

with TRACER.start_as_current_span("agent.route.execute") as span:
    try:
        await handle_message(context)
    except Exception as error:
        status = "error"
        span.record_exception(error)
        span.set_status(Status(StatusCode.ERROR))
        raise
    finally:
        attributes = {
            "route.type": "message",
            "status": status,
        }
        ROUTE_EXECUTIONS.add(1, attributes)
        ROUTE_DURATION.record(
            (time.perf_counter() - started) * 1000,
            attributes,
        )
```

The span context manager ends the span automatically. Do not call `span.end()`
inside the `with` block.

Use normal Python `logging` records for operational logs. The OTel
`LoggingHandler` correlates records emitted in an active context. Keep
sensitive and high-cardinality content out of log messages and `extra`
attributes.

### 9. Filter selected SDK spans only when requested

For centrally managed removal of selected completed spans, prefer the
OpenTelemetry Collector filter processor:

```yaml
processors:
  filter/drop_agents_storage:
    error_mode: ignore
    trace_conditions:
      - span.name == "agents.storage.read"
      - span.name == "agents.storage.write"
      - span.name == "agents.storage.delete"
  batch:

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [filter/drop_agents_storage, batch]
      exporters: [otlp]
```

Collector filtering prevents matching spans from reaching the backend and
centralizes policy, but it does not reduce application or
application-to-Collector overhead. Dropping individual spans can orphan
descendants and produce incomplete traces.

Use an SDK sampler when the goal is to avoid recording and transmitting the
telemetry. Prefer sampling whole traces. If a user explicitly requires
span-name filtering inside the application, wrap the deployment sampler:

```python
from opentelemetry.sdk.trace.sampling import (
    Decision,
    ParentBased,
    Sampler,
    SamplingResult,
    TraceIdRatioBased,
)

SUPPRESSED_SPANS = frozenset(
    {
        "agents.storage.read",
        "agents.storage.write",
        "agents.storage.delete",
    }
)


class SpanFilterSampler(Sampler):
    def __init__(self, delegate: Sampler) -> None:
        self._delegate = delegate

    def should_sample(
        self,
        parent_context,
        trace_id,
        name,
        kind=None,
        attributes=None,
        links=None,
        trace_state=None,
    ):
        if name in SUPPRESSED_SPANS:
            return SamplingResult(Decision.DROP)

        return self._delegate.should_sample(
            parent_context=parent_context,
            trace_id=trace_id,
            name=name,
            kind=kind,
            attributes=attributes,
            links=links,
            trace_state=trace_state,
        )

    def get_description(self) -> str:
        return f"SpanFilterSampler({self._delegate.get_description()})"


sampler = SpanFilterSampler(
    ParentBased(root=TraceIdRatioBased(0.1))
)
tracer_provider = TracerProvider(
    resource=resource,
    sampler=sampler,
)
```

The filter must wrap `ParentBased`; placing the filter only in the
`ParentBased.root` delegate will not filter child SDK spans with a sampled
parent.

Use literal span names in the provider bootstrap. Importing Agents SDK
constant modules there can violate the provider-before-SDK import rule.

Do not implement the default filtering policy in an application
`SpanProcessor`. Processors receive spans that were already recorded, and a
normal processor chain does not let one processor prevent later processors
from receiving the same span.

### 10. Set up the Aspire Dashboard

Copy `assets/start-aspire-dashboard.ps1` from this skill into a local scripts
directory and run:

```powershell
.\start-aspire-dashboard.ps1
```

Open `http://localhost:18888`. The script:

- Binds the UI and OTLP ports to `127.0.0.1`.
- Exposes OTLP/gRPC on `4317` and OTLP/HTTP on `4318`.
- Enables unsecured access only for local development.
- Pulls `mcr.microsoft.com/dotnet/aspire-dashboard:latest`.

Do not start a second container if `aspire-dashboard` already exists. Reuse or
stop the existing container:

```powershell
docker stop aspire-dashboard
```

### 11. Validate end to end

1. Run the project's existing tests, type checker, or import/compile check.
2. Start Aspire Dashboard and confirm the container remains running.
3. Start the exact application module that configures providers before the
   SDK imports.
4. Send at least one message through Agents Playground or a configured
   channel.
   Metrics are exported periodically and can take one or more export intervals,
   plus dashboard processing time, to appear. Wait and refresh the dashboard
   before diagnosing missing metrics.
5. In Aspire Dashboard, confirm:
   - Telemetry is grouped under the application's `service.name`.
   - Traces include `agents.adapter.process` or `agents.app.run`.
   - Metrics include `agents.activities.received`, `agents.turn.count`, or
     another exercised SDK operation.
   - Python logging records appear with trace correlation.
6. Stop the application normally and confirm provider shutdown completes.
7. Exercise a failure path when practical and confirm the span is marked
   `ERROR` while the exception still reaches normal application handling.

See [references/sdk-signals.md](references/sdk-signals.md) for the canonical
Agents SDK spans, metrics, constants, and Aspire smoke test.

## Exporter choices

### Aspire Dashboard or another OTLP collector

Use `opentelemetry-exporter-otlp` and select imports matching the collector's
gRPC or HTTP/protobuf protocol.

### Azure Monitor / Application Insights

When requested, use the current stable `azure-monitor-opentelemetry`
distribution and call `configure_azure_monitor()` before importing the Agents
SDK. Load `APPLICATIONINSIGHTS_CONNECTION_STRING` from a secret source.

The Azure Monitor distribution owns provider configuration. Do not also run
the complete manual provider template unless the current Azure Monitor
documentation explicitly supports that combination. Preserve OTLP only when
dual export is intentional.

### Existing logging configuration

Preserve the application's formatters, filters, levels, and handlers. Add one
OTel `LoggingHandler` or use the exporter integration already provided by the
selected distribution. Do not call `logging.basicConfig()` merely to enable
OTel.

## Troubleshooting

| Symptom | Check |
|---|---|
| No SDK spans or metrics | Providers were configured after importing `microsoft_agents.hosting.core` |
| Provider override warning | More than one module calls a global `set_*_provider` function |
| aiohttp spans missing | Instrumentors ran after application creation or requests began |
| Custom telemetry missing | Tracer/meter helper was imported before provider setup |
| Logs missing | One `LoggingHandler` and a batch log processor are configured |
| Duplicate logs | Multiple OTel handlers were attached to the same logger hierarchy |
| Dashboard empty | Endpoint is `http://localhost:4317`; exporter and protocol are gRPC |
| Traces appear but metrics do not | Metrics can take one or more `OTEL_METRIC_EXPORT_INTERVAL` periods plus dashboard processing time to appear; wait and refresh before troubleshooting |
| OTLP protocol error | Exporter import, endpoint port, and protocol do not match |
| Last telemetry is absent | All three providers are shut down during graceful cleanup |
| High telemetry cost | Remove high-cardinality attributes and reduce trace sampling |
| Filtered child spans still appear | The filter was placed inside, rather than around, `ParentBased` |

## Contributing

If SDK signals differ from [references/sdk-signals.md](references/sdk-signals.md),
the sample no longer runs, or the Aspire setup changes, draft an issue
containing Python/package versions, startup import order, host framework,
expected signal, actual result, and any workaround:
https://github.com/microsoft/agents/issues/new

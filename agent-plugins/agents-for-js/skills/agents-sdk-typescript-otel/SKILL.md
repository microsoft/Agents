---
name: agents-sdk-typescript-otel
description: >
  Use when adding, configuring, validating, or troubleshooting OpenTelemetry
  observability for a Microsoft 365 Agents SDK application in JavaScript or
  TypeScript. Trigger when the user mentions OpenTelemetry, OTel, telemetry,
  traces, metrics, logs, OTLP, Aspire Dashboard, Application Insights, Azure
  Monitor, distributed tracing, custom tracers or meters, or observing
  @microsoft/agents-telemetry signals.
---

# OpenTelemetry for Agents SDK (JavaScript/TypeScript)

## Goal

Configure traces, metrics, and logs for an Agents SDK application, including
the SDK's built-in telemetry and a local Aspire Dashboard. Make the smallest
complete change that fits the project's module system, build process, package
manager, and existing observability architecture.

## Non-negotiable rules

- Inspect the project before editing. Extend an existing OpenTelemetry SDK
  registration instead of creating a second global provider.
- Start the OpenTelemetry SDK before importing the Agents SDK, HTTP, Express,
  or the application entry point. Preload instrumentation with Node's
  `--import` or `--require` option.
- Use a stable application-specific `service.name`.
- Let OTLP exporters read standard environment variables. Never embed
  collector credentials, API keys, or connection strings in source.
- Do not force all OpenTelemetry packages to the same version. The JavaScript
  package family intentionally mixes stable API/SDK versions and `0.x`
  exporter/instrumentation versions. Install a compatible set together and
  preserve the lockfile.
- Never add message text, prompts, responses, tokens, authorization headers,
  cookies, secrets, attachment contents, user names, user IDs, conversation
  IDs, activity IDs, or arbitrary HTTP headers to custom telemetry by default.
- Keep custom metric attributes low-cardinality. Good dimensions include
  route type, activity type, channel ID, operation, status, and error type.
- Do not configure always-on sampling as a production default. Honor
  `OTEL_TRACES_SAMPLER` and `OTEL_TRACES_SAMPLER_ARG`.
- Explicitly flush telemetry during shutdown. Do not silently ignore exporter
  or shutdown errors.
- Aspire Dashboard anonymous mode is local-development only. Bind it to
  loopback and never deploy that configuration.
- Telemetry must record and rethrow application errors, not swallow them.

## Workflow

Complete these steps in order.

### 1. Inspect the application

Find:

- `package.json`, the lockfile, Node engine version, and package manager.
- Whether the project uses ESM (`"type": "module"`) or CommonJS.
- The TypeScript output directory and current start/build scripts.
- The application entry point and whether it uses `startServer()`, Express, or
  another host.
- Existing imports from `@opentelemetry/*`,
  `@microsoft/agents-telemetry`, or logging integrations.
- Existing `NodeSDK`, tracer provider, meter provider, logger provider,
  exporters, processors, instrumentations, and shutdown hooks.
- Existing OTEL settings in `.env` templates, deployment manifests,
  container definitions, and CI/CD configuration.

If observability already exists, add the missing Agents SDK-compatible global
providers and keep the existing exporters and instrumentations unless the user
requested a change.

### 2. Add a compatible package set

Use the repository's package manager. For npm:

```powershell
npm install @opentelemetry/api @opentelemetry/api-logs `
  @microsoft/agents-telemetry `
  @opentelemetry/sdk-node @opentelemetry/sdk-metrics `
  @opentelemetry/sdk-logs @opentelemetry/sdk-trace-base `
  @opentelemetry/exporter-trace-otlp-grpc `
  @opentelemetry/exporter-metrics-otlp-grpc `
  @opentelemetry/exporter-logs-otlp-grpc `
  @opentelemetry/instrumentation-http `
  @opentelemetry/resources @opentelemetry/semantic-conventions
```

Adapt the command for pnpm or yarn. Install the packages in one operation so
the package manager can resolve compatible peers. Do not manually rewrite the
resolved versions into one shared version number.

`@microsoft/agents-telemetry` is required as a direct dependency when
application code imports `SpanNames` or `MetricNames`; do not rely on the
Agents SDK's transitive copy.

Use OTLP/HTTP exporter packages instead of the gRPC exporters only when the
collector requires `http/protobuf`.

### 3. Create the preload instrumentation module

Create `src/instrumentation.ts` or the equivalent path. Adapt the default
service name and version:

```typescript
import { randomUUID } from 'node:crypto'
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-grpc'
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-grpc'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-grpc'
import { HttpInstrumentation } from '@opentelemetry/instrumentation-http'
import { resourceFromAttributes } from '@opentelemetry/resources'
import {
  BatchLogRecordProcessor,
  type LogRecordExporter
} from '@opentelemetry/sdk-logs'
import {
  PeriodicExportingMetricReader,
  type PushMetricExporter
} from '@opentelemetry/sdk-metrics'
import { NodeSDK } from '@opentelemetry/sdk-node'
import type { SpanExporter } from '@opentelemetry/sdk-trace-base'
import {
  ATTR_SERVICE_INSTANCE_ID,
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION
} from '@opentelemetry/semantic-conventions'

const serviceName =
  process.env.OTEL_SERVICE_NAME ?? 'contoso-support-agent'
const serviceVersion =
  process.env.npm_package_version ?? '1.0.0'

const traceExporter: SpanExporter = new OTLPTraceExporter()
const metricExporter: PushMetricExporter = new OTLPMetricExporter()
const logExporter: LogRecordExporter = new OTLPLogExporter()

export const otelSdk = new NodeSDK({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: serviceName,
    [ATTR_SERVICE_VERSION]: serviceVersion,
    [ATTR_SERVICE_INSTANCE_ID]: randomUUID()
  }),
  traceExporter,
  metricReader: new PeriodicExportingMetricReader({
    exporter: metricExporter
  }),
  logRecordProcessors: [
    new BatchLogRecordProcessor(logExporter)
  ],
  instrumentations: [
    new HttpInstrumentation({
      ignoreIncomingRequestHook: request =>
        request.url?.startsWith('/health') === true
    })
  ]
})

otelSdk.start()

let shutdownStarted = false

const shutdown = async (signal: NodeJS.Signals): Promise<void> => {
  if (shutdownStarted) {
    return
  }

  shutdownStarted = true

  try {
    await otelSdk.shutdown()
  } catch (error) {
    console.error('Failed to shut down OpenTelemetry', error)
    process.exitCode = 1
  } finally {
    process.kill(process.pid, signal)
  }
}

process.once('SIGTERM', signal => {
  void shutdown(signal)
})

process.once('SIGINT', signal => {
  void shutdown(signal)
})
```

Before using the signal-handler example, check the application's existing
shutdown behavior. Prefer integrating `otelSdk.shutdown()` into the existing
graceful shutdown path so the HTTP server, storage, and telemetry all close in
the correct order. Do not register duplicate signal handlers.

If the installed semantic-conventions package does not expose
`ATTR_SERVICE_INSTANCE_ID`, use the literal stable semantic key
`'service.instance.id'`.

The `NodeSDK` installs global trace, metric, context, propagation, and log
providers. Agents SDK telemetry uses those global APIs automatically; no
Agents-specific instrumentation registration is required.

### 4. Preload instrumentation before the application

For an ESM TypeScript project that compiles to `dist`:

```json
{
  "scripts": {
    "build": "tsc --build",
    "prestart": "npm run build",
    "start": "node --env-file .env --import ./dist/instrumentation.js ./dist/agent.js"
  }
}
```

For CommonJS, use `--require`:

```json
"start": "node --require ./dist/instrumentation.cjs ./dist/agent.cjs"
```

Do not import `instrumentation.ts` from the bottom of the application entry
point. By then, HTTP or framework modules may already be loaded and cannot be
patched reliably.

If a test runner, worker, background process, or alternate start script
executes the agent independently, preload instrumentation there too when that
process should emit telemetry.

### 5. Configure OTLP

For the local Aspire Dashboard using OTLP/gRPC, add non-secret defaults to the
uncommitted `.env` and document them in `.env.example` or `env.TEMPLATE`:

```dotenv
OTEL_SERVICE_NAME=contoso-support-agent
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

Standard environment variables to support:

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
| `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` | Optional SDK span-category filter |

Use deployment secrets for `OTEL_EXPORTER_OTLP_HEADERS` and Azure Monitor
connection strings. Do not commit populated `.env` files.

### 6. Add custom business telemetry only when useful

The Agents SDK already emits adapter, application, turn, connector, storage,
authentication, authorization, proactive, dialog, and Copilot Studio signals.
Add custom telemetry only for application-specific operations.

Create one shared helper:

```typescript
import { metrics, trace } from '@opentelemetry/api'
import { logs, SeverityNumber } from '@opentelemetry/api-logs'

const sourceName = 'Contoso.SupportAgent'
const version = process.env.npm_package_version

export const agentTracer = trace.getTracer(sourceName, version)
export const agentMeter = metrics.getMeter(sourceName, version)
export const agentLogger = logs.getLogger(sourceName, version)

export const routeExecutions = agentMeter.createCounter(
  'agent.route.executions',
  {
    unit: '{execution}',
    description: 'Number of application route executions.'
  })

export const routeDuration = agentMeter.createHistogram(
  'agent.route.duration',
  {
    unit: 'ms',
    description: 'Application route execution duration.'
  })

export const emitErrorLog = (
  message: string,
  attributes: Record<string, string | number | boolean>
): void => {
  agentLogger.emit({
    severityNumber: SeverityNumber.ERROR,
    severityText: 'ERROR',
    body: message,
    attributes
  })
}
```

Instrument a route with bounded attributes:

```typescript
import { SpanStatusCode } from '@opentelemetry/api'

return agentTracer.startActiveSpan(
  'agent.route.execute',
  async span => {
    const started = performance.now()
    let status = 'ok'

    try {
      await handleMessage(context)
    } catch (error) {
      status = 'error'
      const exception =
        error instanceof Error ? error : new Error(String(error))

      span.recordException(exception)
      span.setStatus({
        code: SpanStatusCode.ERROR,
        message: exception.message
      })
      throw error
    } finally {
      const attributes = {
        'route.type': 'message',
        status
      }

      routeExecutions.add(1, attributes)
      routeDuration.record(performance.now() - started, attributes)
      span.end()
    }
  })
```

Do not add conversation, activity, user, prompt, response, URL, or exception
message values to custom metrics. Use the standard application logger or the
OpenTelemetry Logs API for operational messages, but keep sensitive content
out of both.

### 7. Control SDK span categories when needed

All built-in categories are enabled by default. To reduce trace volume:

```dotenv
AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES=STORAGE,AUTHORIZATION
```

Valid values are:

- `STORAGE`
- `AUTHENTICATION`
- `AUTHORIZATION`
- `DIALOGS`

Disable a category only after confirming those spans are not needed for
diagnosis, security auditing, or service-level objectives.

### 8. Set up the Aspire Dashboard

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

### 9. Validate end to end

1. Run the project's existing build or type-check command.
2. Start Aspire Dashboard and confirm its container remains running.
3. Start the agent through the script that preloads instrumentation.
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
   - Direct OpenTelemetry log records appear.
6. Stop the application normally and confirm telemetry shutdown completes.
7. Exercise a failure path when practical and confirm the error is recorded
   while the original error behavior remains intact.

See [references/sdk-signals.md](references/sdk-signals.md) for the canonical
Agents SDK spans, metrics, constants, and Aspire smoke test.

## Exporter choices

### Aspire Dashboard or another OTLP collector

Use the OTLP exporter packages matching the collector protocol. gRPC is the
default for the included Aspire Dashboard setup.

### Azure Monitor / Application Insights

When requested, use the current supported Azure Monitor OpenTelemetry exporter
for Node.js and load its connection string from a secret source. Check the
exporter's current initialization and compatibility documentation before
editing because Azure Monitor's JavaScript OpenTelemetry integration evolves
independently from the core OTel packages. Preserve OTLP only when dual export
is intentional.

### Existing application loggers

OpenTelemetry does not automatically export every `console`, pino, Winston, or
other logger record. Use a compatible instrumentation/transport for the
project's logger, or emit selected structured records through
`@opentelemetry/api-logs`. Do not replace the application's logging framework
solely to add telemetry.

## Troubleshooting

| Symptom | Check |
|---|---|
| No SDK spans or metrics | `NodeSDK.start()` ran before the Agents SDK was imported |
| HTTP spans missing | Instrumentation was preloaded before `node:http`, Express, and the application |
| Custom telemetry missing | The tracer, meter, and logger are obtained after the global SDK starts |
| Logs missing | A log record processor/exporter exists and the app emits through an integrated logger |
| Dashboard empty | Endpoint is `http://localhost:4317`; exporter and protocol are both gRPC |
| Traces appear but metrics do not | Metrics can take one or more `OTEL_METRIC_EXPORT_INTERVAL` periods plus dashboard processing time to appear; wait and refresh before troubleshooting |
| OTLP protocol error | Exporter package, endpoint port, and protocol do not match |
| Duplicate telemetry | More than one SDK/provider or duplicate instrumentation preload exists |
| Process hangs during shutdown | Integrate OTel shutdown with the server's existing graceful shutdown |
| Last spans or logs are absent | Await `sdk.shutdown()` before process exit |
| High telemetry cost | Remove high-cardinality attributes and reduce trace sampling |
| Some SDK spans are absent | Check `AGENTS_TELEMETRY_DISABLED_SPAN_CATEGORIES` |

## Contributing

If SDK signals differ from [references/sdk-signals.md](references/sdk-signals.md),
the sample no longer builds, or the Aspire setup changes, draft an issue
containing the module system, Node and package versions, start command,
expected signal, actual result, and any workaround:
https://github.com/microsoft/agents/issues/new

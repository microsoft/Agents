---
name: agents-sdk-dotnet-otel
description: >
  Use when adding, configuring, validating, or troubleshooting OpenTelemetry
  observability for a Microsoft 365 Agents SDK application in C# / .NET.
  Trigger when the user mentions OpenTelemetry, OTel, telemetry, traces,
  metrics, logs, OTLP, Aspire Dashboard, Application Insights, Azure Monitor,
  distributed tracing, custom ActivitySource or Meter instrumentation, or
  observing Microsoft.Agents.Core signals.
---

# OpenTelemetry for Agents SDK (.NET)

## Goal

Configure traces, metrics, and logs for an Agents SDK application, including
the SDK's built-in `Microsoft.Agents.Core` signals and a local Aspire Dashboard.
Make the smallest complete change that fits the existing hosting and telemetry
architecture.

Use this skill for both `AgentApplication` and `ActivityHandler` applications.
The SDK telemetry source is the same for both.

## Non-negotiable rules

- Inspect the project before editing. Reuse and extend an existing
  `AddOpenTelemetry()` registration instead of creating competing providers.
- Register telemetry before `builder.Build()` and before the app processes
  agent requests.
- Subscribe to both `AgentsTelemetry.SourceName` and
  `AgentsTelemetry.Meter`; the SDK has no direct OpenTelemetry dependency and
  publishes through `System.Diagnostics`.
- Give the application its own stable `service.name`. Do not use
  `Microsoft.Agents.Core` as the service name; that is an instrumentation
  source, not the application's identity.
- Use OTLP environment variables rather than embedding collector URLs,
  headers, API keys, or connection strings in source code.
- Never capture message text, prompts, responses, access tokens,
  authorization headers, cookies, secrets, attachment contents, user names,
  user IDs, conversation IDs, activity IDs, or arbitrary HTTP headers by
  default.
- Keep metric attributes low-cardinality. Good dimensions include route type,
  activity type, channel ID, operation, status, and error type. Do not put
  per-user, per-conversation, per-message, URL, or exception-message values on
  metrics.
- Do not add `AlwaysOnSampler` as a production default. Honor the standard
  `OTEL_TRACES_SAMPLER` and `OTEL_TRACES_SAMPLER_ARG` configuration.
- Aspire Dashboard anonymous mode is local-development only. Bind its ports to
  loopback and do not deploy that configuration.
- Telemetry must never swallow application exceptions or turn failures into
  successful results.

## Workflow

Complete these steps in order.

### 1. Inspect the application

Find:

- The web or generic-host project (`*.csproj`) and target framework.
- `Program.cs` or the host-builder entry point.
- Existing calls to `AddOpenTelemetry`, `ConfigureOpenTelemetry`,
  `UseAzureMonitor`, `AddOtlpExporter`, `AddApplicationInsightsTelemetry`, or
  custom observability extensions.
- Existing `ActivitySource`, `Meter`, counters, histograms, and logging setup.
- Existing OTEL settings in `appsettings*.json`, launch profiles, environment
  files, container definitions, or deployment manifests.
- Whether package versions are centrally managed in
  `Directory.Packages.props`.

If OpenTelemetry already exists, merge the Agents SDK source and meter into
that registration. Do not replace existing exporters or instrumentation
without a reason.

### 2. Add only the required packages

Use the repository's package-management convention. Keep all OpenTelemetry
packages on a compatible stable version. When versions are not centrally
managed, these commands select the latest stable packages:

```powershell
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.Runtime
```

Do not add the console exporter unless the user explicitly wants console
telemetry. Do not add Azure Monitor packages unless Azure Monitor export is
requested.

### 3. Configure the providers

Prefer a project-local extension such as `AgentOtelExtensions.cs`. Adapt the
namespace and service name to the project:

```csharp
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MyAgent;

public static class AgentOtelExtensions
{
    public static TBuilder AddAgentObservability<TBuilder>(
        this TBuilder builder,
        string serviceName,
        string? serviceVersion = null,
        string? applicationTelemetrySource = null)
        where TBuilder : IHostApplicationBuilder
    {
        string resolvedServiceName =
            builder.Configuration["OTEL_SERVICE_NAME"] ?? serviceName;

        var openTelemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: resolvedServiceName,
                serviceVersion: serviceVersion));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(AgentsTelemetry.SourceName)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddOtlpExporter();

            if (!string.IsNullOrWhiteSpace(applicationTelemetrySource))
            {
                tracing.AddSource(applicationTelemetrySource);
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddMeter(AgentsTelemetry.SourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter();

            if (!string.IsNullOrWhiteSpace(applicationTelemetrySource))
            {
                metrics.AddMeter(applicationTelemetrySource);
            }
        });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        return builder;
    }
}
```

Notes:

- If the project does not expose `/health`, keeping the filter is harmless.
- If an existing telemetry extension already adds ASP.NET Core, HTTP client,
  runtime, or OTLP instrumentation, add only the missing
  `AgentsTelemetry.SourceName` source and meter.
- Do not manually add `"Microsoft.AspNetCore"` or `"System.Net.Http"` activity
  source strings when the corresponding instrumentation packages are already
  configured.
- `service.name` should identify the application, for example
  `contoso-support-agent`. Keep it stable across instances.
- Use `service.version` for the deployed build/version when one is available.
- Use resource attributes such as `deployment.environment.name` for stable
  deployment metadata, not request-specific data.

Call the extension before the agent registrations:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentObservability(
    serviceName: "contoso-support-agent",
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
    applicationTelemetrySource: AgentTelemetry.SourceName);

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(options => options.AddAgentAspNetAuthentication());
```

If no custom application telemetry is needed, omit
`applicationTelemetrySource` and do not create `AgentTelemetry`.

### 4. Configure OTLP without secrets in source

For a local Aspire Dashboard using OTLP/gRPC:

```powershell
$env:OTEL_SERVICE_NAME = "contoso-support-agent"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
dotnet run
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

Use user secrets, a secret store, or deployment secrets for
`OTEL_EXPORTER_OTLP_HEADERS` and Azure Monitor connection strings. Never place
them in committed `appsettings.json` or launch profiles.

### 5. Add custom business telemetry only when useful

The Agents SDK already emits adapter, turn, route, connector, storage,
authentication, and authorization signals. Add custom telemetry for
application-specific operations, not to duplicate those SDK spans.

Use one shared source name for the application's custom traces and metrics:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyAgent;

public static class AgentTelemetry
{
    public const string SourceName = "Contoso.SupportAgent";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> RouteExecutions =
        Meter.CreateCounter<long>(
            "agent.route.executions",
            unit: "{execution}",
            description: "Number of application route executions.");

    public static readonly Histogram<double> RouteDuration =
        Meter.CreateHistogram<double>(
            "agent.route.duration",
            unit: "ms",
            description: "Application route execution duration.");
}
```

Instrument a handler with bounded attributes:

```csharp
using Activity? activity =
    AgentTelemetry.ActivitySource.StartActivity("agent.route.execute");
long started = Stopwatch.GetTimestamp();
string status = "ok";

try
{
    await HandleMessageAsync(turnContext, cancellationToken);
}
catch (Exception exception)
{
    status = "error";
    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    activity?.AddException(exception);
    throw;
}
finally
{
    TagList tags = new()
    {
        { "route.type", "message" },
        { "status", status }
    };

    AgentTelemetry.RouteExecutions.Add(1, tags);
    AgentTelemetry.RouteDuration.Record(
        Stopwatch.GetElapsedTime(started).TotalMilliseconds,
        tags);
}
```

If `AddException` is unavailable for the project's package version, add an
`exception` activity event using the OpenTelemetry semantic attributes. Do not
log or tag the prompt, response, or exception message as a metric dimension.

### 6. Set up the Aspire Dashboard

Copy `assets/start-aspire-dashboard.ps1` from this skill into a suitable local
development or scripts directory. The script pulls
`mcr.microsoft.com/dotnet/aspire-dashboard:latest`.

Run:

```powershell
.\start-aspire-dashboard.ps1
```

Then open `http://localhost:18888`. The script:

- Binds the UI and OTLP ports to `127.0.0.1`.
- Exposes OTLP/gRPC on `4317` and OTLP/HTTP on `4318`.
- Enables unsecured access only for local development.
- Runs the dashboard in a disposable Docker container.

Do not start a second container if `aspire-dashboard` already exists. Inspect
or reuse the existing container instead. Stop the generated local dashboard
with:

```powershell
docker stop aspire-dashboard
```

### 7. Validate end to end

1. Run the smallest applicable `dotnet build`.
2. Start the Aspire Dashboard and confirm the container remains running.
3. Start the agent with the OTLP environment variables.
4. Send at least one message through the Agents Playground or configured
   channel.
   Metrics are exported periodically and can take one or more export intervals,
   plus dashboard processing time, to appear. Wait and refresh the dashboard
   before diagnosing missing metrics.
5. In Aspire Dashboard, confirm:
   - The resource uses the application's `service.name`.
   - Traces include `agents.adapter.process` or `agents.app.run`.
   - Metrics include `agents.activities.received`, `agents.turn.count`, or
     another operation exercised by the test.
   - Application logs appear and correlate with traces when trace context is
     present.
6. Exercise one failure path when practical and confirm the span is marked
   `Error` while the original exception behavior remains intact.

See [references/sdk-signals.md](references/sdk-signals.md) for the canonical
Agents SDK source, common spans, metrics, and dashboard checks.

## Exporter choices

### Aspire Dashboard or another OTLP collector

Use `OpenTelemetry.Exporter.OpenTelemetryProtocol` and `AddOtlpExporter()`.
This is the default for local development and vendor-neutral deployments.

### Azure Monitor / Application Insights

When requested, add the stable
`Azure.Monitor.OpenTelemetry.AspNetCore` package and configure
`UseAzureMonitor()` using `APPLICATIONINSIGHTS_CONNECTION_STRING` from a
secret source. Preserve OTLP export only when dual export is intentional.
Do not add both exporters accidentally.

### Console

Use only for short-lived local diagnosis. Console telemetry is noisy, can
expose data in terminal logs, and is not a production exporter.

## Troubleshooting

| Symptom | Check |
|---|---|
| No SDK spans | `.AddSource(AgentsTelemetry.SourceName)` is present before requests |
| No SDK metrics | `.AddMeter(AgentsTelemetry.SourceName)` is present |
| Custom spans missing | The custom `ActivitySource` name exactly matches `.AddSource(...)` |
| Custom metrics missing | The custom `Meter` name exactly matches `.AddMeter(...)` |
| Dashboard has no resource | Dashboard is running; endpoint is `http://localhost:4317`; protocol is `grpc` |
| Traces appear but metrics do not | Metrics can take one or more `OTEL_METRIC_EXPORT_INTERVAL` periods plus dashboard processing time to appear; wait and refresh before troubleshooting |
| OTLP `Unimplemented` or protocol errors | Collector port and `OTEL_EXPORTER_OTLP_PROTOCOL` do not match |
| Duplicate spans or metrics | Multiple providers or repeated instrumentation registrations exist |
| High memory or telemetry cost | Remove high-cardinality tags and reduce trace sampling |
| Logs appear but traces do not | Trace source subscription or sampling configuration is wrong |
| Traces appear but logs do not | `builder.Logging.AddOpenTelemetry(...).AddOtlpExporter()` is missing |
| Container name conflict | Reuse or stop the existing `aspire-dashboard` container |

## Contributing

If the SDK signals differ from
[references/sdk-signals.md](references/sdk-signals.md), the sample no longer
builds, or the Aspire setup changes, draft an issue containing the project
type, package versions, configuration, expected signal, actual result, and any
workaround: https://github.com/microsoft/agents/issues/new

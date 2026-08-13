// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// OpenTelemetry startup extensions for the ProductionReference application.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Configures OpenTelemetry tracing, metrics, and logging for the ProductionReference service.
    /// Exports to OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, and to Azure Monitor when
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set.
    /// </summary>
    public static WebApplicationBuilder ConfigureProductionReferenceTelemetry(
        this WebApplicationBuilder builder)
    {
        string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        string? appInsightsConnStr = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        bool useOtlp = !string.IsNullOrEmpty(otlpEndpoint);
        bool useAzureMonitor = !string.IsNullOrEmpty(appInsightsConnStr);

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("ProductionReference"))
            .WithTracing(tracing =>
            {
                tracing.AddSource(AgentsTelemetry.SourceName);

                // Azure Monitor distro supplies ASP.NET Core and HttpClient instrumentation.
                // Register those manually only for the OTLP-only path to avoid duplicate spans.
                if (!useAzureMonitor)
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();
                }

                if (useOtlp)
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddRuntimeInstrumentation()
                    .AddMeter(AgentsTelemetry.SourceName);

                // Azure Monitor distro supplies ASP.NET Core and HttpClient instrumentation.
                // Register those manually only for the OTLP-only path to avoid duplicate metric series.
                if (!useAzureMonitor)
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();
                }

                if (useOtlp)
                    metrics.AddOtlpExporter();
            })
            .WithLogging(
                logging =>
                {
                    if (useOtlp)
                        logging.AddOtlpExporter();
                },
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

        if (useAzureMonitor)
            otel.UseAzureMonitor();

        return builder;
    }
}


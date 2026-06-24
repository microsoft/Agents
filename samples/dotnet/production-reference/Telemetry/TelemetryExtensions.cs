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

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("ProductionReference"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(AgentsTelemetry.SourceName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AgentsTelemetry.SourceName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    metrics.AddOtlpExporter();
            })
            .WithLogging(
                logging =>
                {
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                        logging.AddOtlpExporter();
                },
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

        if (!string.IsNullOrEmpty(appInsightsConnStr))
            otel.UseAzureMonitor();

        return builder;
    }
}

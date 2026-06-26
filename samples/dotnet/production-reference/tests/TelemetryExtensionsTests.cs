// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProductionReference.Tests;

public class TelemetryExtensionsTests
{
    [Fact]
    public void ConfigureProductionReferenceTelemetry_registers_http_instrumentation_for_otlp_only()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(Array.Empty<string>());

        builder.ConfigureProductionReferenceTelemetry();

        Assert.Equal(1, CountHttpClientInstrumentationOptions(builder.Services));
    }

    [Fact]
    public void ConfigureProductionReferenceTelemetry_does_not_duplicate_http_instrumentation_when_azure_monitor_is_enabled()
    {
        WebApplicationBuilder baseline = CreateAzureMonitorBuilder();
        baseline.Services.AddOpenTelemetry().UseAzureMonitor();

        WebApplicationBuilder builder = CreateAzureMonitorBuilder();
        builder.ConfigureProductionReferenceTelemetry();

        Assert.Equal(
            CountOpenTelemetryBuilderCallbacks(baseline.Services, "OpenTelemetry.Trace.IConfigureTracerProviderBuilder") + 4,
            CountOpenTelemetryBuilderCallbacks(builder.Services, "OpenTelemetry.Trace.IConfigureTracerProviderBuilder"));
        Assert.Equal(
            CountOpenTelemetryBuilderCallbacks(baseline.Services, "OpenTelemetry.Metrics.IConfigureMeterProviderBuilder") + 6,
            CountOpenTelemetryBuilderCallbacks(builder.Services, "OpenTelemetry.Metrics.IConfigureMeterProviderBuilder"));
    }

    private static WebApplicationBuilder CreateAzureMonitorBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.com/",
        });
        return builder;
    }

    private static int CountHttpClientInstrumentationOptions(IServiceCollection services)
    {
        return services.Count(service =>
            service.ServiceType.FullName?.Contains("HttpClientTraceInstrumentationOptions", StringComparison.Ordinal) == true);
    }

    private static int CountOpenTelemetryBuilderCallbacks(IServiceCollection services, string serviceTypeName)
    {
        return services.Count(service =>
            string.Equals(service.ServiceType.FullName, serviceTypeName, StringComparison.Ordinal));
    }
}


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ProductionReference.Tests;

/// <summary>
/// Integration tests for health check endpoints and unit tests for
/// <see cref="StorageConfigurationHealthCheck"/> configuration validation.
/// </summary>
public class HealthEndpointTests : IClassFixture<HealthEndpointTests.Factory>
{
    /// <summary>
    /// Sets the environment to "Test" so that <c>appsettings.Test.json</c> is loaded.
    /// This provides <c>AZURE_BLOB_STORAGE_CONNECTION_STRING</c> before startup validation
    /// runs in <c>Program.cs</c>, without requiring a live Azure dependency.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
        }
    }

    private readonly Factory _factory;

    public HealthEndpointTests(Factory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_Returns200_WithoutAzureDependencies()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StorageConfigurationHealthCheck_Unhealthy_WhenNoStorageConfigured()
    {
        var options = new StorageOptions();
        var check = new StorageConfigurationHealthCheck(options);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "storage_configuration", check, HealthStatus.Unhealthy, null),
        };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task StorageConfigurationHealthCheck_Healthy_WhenConnectionStringConfigured()
    {
        var options = new StorageOptions { ConnectionString = "UseDevelopmentStorage=true" };
        var check = new StorageConfigurationHealthCheck(options);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "storage_configuration", check, HealthStatus.Unhealthy, null),
        };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task StorageConfigurationHealthCheck_Healthy_WhenContainerUriConfigured()
    {
        var options = new StorageOptions
        {
            ContainerUri = new Uri("https://account.blob.core.windows.net/container"),
        };
        var check = new StorageConfigurationHealthCheck(options);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "storage_configuration", check, HealthStatus.Unhealthy, null),
        };

        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}

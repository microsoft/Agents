// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
    /// Sets <c>AZURE_BLOB_STORAGE_CONNECTION_STRING</c> as a process environment variable
    /// before the <see cref="WebApplicationFactory{TEntryPoint}"/> starts the app, and
    /// restores the prior value on disposal.
    /// <para>
    /// This is necessary because <c>Program.cs</c> calls <c>storageOptions.Validate()</c>
    /// during <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/> construction
    /// before any <c>ConfigureWebHost</c> callback can inject configuration, so the value
    /// must already be visible via <see cref="System.Environment.GetEnvironmentVariable"/> at
    /// that point.  The environment variable is scoped to the current process and is restored
    /// on disposal, so it does not leak into other test classes.
    /// </para>
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private const string ConnectionStringKey = "AZURE_BLOB_STORAGE_CONNECTION_STRING";
        private readonly string? _previousValue;

        public Factory()
        {
            _previousValue = Environment.GetEnvironmentVariable(ConnectionStringKey);
            Environment.SetEnvironmentVariable(ConnectionStringKey, "UseDevelopmentStorage=true");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Environment.SetEnvironmentVariable(ConnectionStringKey, _previousValue);
            }
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
    public async Task ReadinessEndpoint_Returns200_WhenStorageConfigurationIsValid()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
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


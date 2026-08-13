// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Reports readiness based on whether <see cref="StorageOptions"/> contains a valid configuration
/// shape.  Does not make any live Azure Storage calls.
/// </summary>
public sealed class StorageConfigurationHealthCheck : IHealthCheck
{
    private readonly StorageOptions _options;

    public StorageConfigurationHealthCheck(StorageOptions options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _options.Validate();
            return Task.FromResult(HealthCheckResult.Healthy("Storage configuration is valid."));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage configuration is invalid.", ex));
        }
    }
}

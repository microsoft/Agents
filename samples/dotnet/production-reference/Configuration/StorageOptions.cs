// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

/// <summary>
/// Configuration options for Azure Blob Storage backing the agent state store.
/// Exactly one of <see cref="ConnectionString"/> or <see cref="ContainerUri"/> must be set.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Azure Blob Storage connection string.
    /// Set via the <c>AZURE_BLOB_STORAGE_CONNECTION_STRING</c> configuration key.
    /// Mutually exclusive with <see cref="ContainerUri"/>.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Absolute URI to the Azure Blob Storage container, used with managed identity.
    /// Set via the <c>AZURE_BLOB_STORAGE_CONTAINER_URI</c> configuration key.
    /// Mutually exclusive with <see cref="ConnectionString"/>.
    /// </summary>
    public Uri? ContainerUri { get; init; }

    /// <summary>
    /// Name of the blob container.  Only used with <see cref="ConnectionString"/>.
    /// Set via the <c>AZURE_BLOB_STORAGE_CONTAINER_NAME</c> configuration key.
    /// Defaults to <c>"agents-production-reference-state"</c>.
    /// </summary>
    public string ContainerName { get; init; } = "agents-production-reference-state";

    /// <summary>
    /// Validates the options and throws <see cref="InvalidOperationException"/> if misconfigured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither or both of <see cref="ConnectionString"/> and <see cref="ContainerUri"/> are set,
    /// or when <see cref="ContainerName"/> is empty while using <see cref="ConnectionString"/>.
    /// </exception>
    public void Validate()
    {
        bool hasConnectionString = !string.IsNullOrEmpty(ConnectionString);
        bool hasContainerUri = ContainerUri is not null;

        if (!hasConnectionString && !hasContainerUri)
        {
            throw new InvalidOperationException(
                "Storage configuration is required. Set exactly one of " +
                "AZURE_BLOB_STORAGE_CONNECTION_STRING or AZURE_BLOB_STORAGE_CONTAINER_URI.");
        }

        if (hasConnectionString && hasContainerUri)
        {
            throw new InvalidOperationException(
                "AZURE_BLOB_STORAGE_CONNECTION_STRING and AZURE_BLOB_STORAGE_CONTAINER_URI " +
                "are mutually exclusive. Set exactly one.");
        }

        if (hasConnectionString && string.IsNullOrEmpty(ContainerName))
        {
            throw new InvalidOperationException(
                "AZURE_BLOB_STORAGE_CONTAINER_NAME is required when using AZURE_BLOB_STORAGE_CONNECTION_STRING.");
        }
    }
}

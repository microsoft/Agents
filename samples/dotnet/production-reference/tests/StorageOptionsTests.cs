// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Xunit;

namespace ProductionReference.Tests;

public class StorageOptionsTests
{
    [Fact]
    public void Validate_fails_when_neither_connection_string_nor_container_uri_is_set()
    {
        var options = new StorageOptions();

        var ex = Assert.Throws<InvalidOperationException>((Action)(() => options.Validate()));

        Assert.Contains("AZURE_BLOB_STORAGE_CONNECTION_STRING", ex.Message);
        Assert.Contains("AZURE_BLOB_STORAGE_CONTAINER_URI", ex.Message);
    }

    [Fact]
    public void Validate_fails_when_both_connection_string_and_container_uri_are_set()
    {
        var options = new StorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerUri = new Uri("https://account.blob.core.windows.net/container"),
        };

        var ex = Assert.Throws<InvalidOperationException>((Action)(() => options.Validate()));

        Assert.Contains("AZURE_BLOB_STORAGE_CONNECTION_STRING", ex.Message);
        Assert.Contains("AZURE_BLOB_STORAGE_CONTAINER_URI", ex.Message);
    }

    [Fact]
    public void Validate_allows_local_connection_string_only()
    {
        var options = new StorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
        };

        // Should not throw.
        options.Validate();
    }

    [Fact]
    public void Validate_allows_managed_identity_container_uri_only()
    {
        var options = new StorageOptions
        {
            ContainerUri = new Uri("https://account.blob.core.windows.net/container"),
        };

        // Should not throw.
        options.Validate();
    }
}

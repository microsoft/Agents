// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Bind and validate storage configuration.  Fails fast on startup if no valid
// storage configuration is provided; there is no in-memory fallback.
StorageOptions storageOptions = new()
{
    ConnectionString = builder.Configuration["AZURE_BLOB_STORAGE_CONNECTION_STRING"],
    ContainerName = builder.Configuration["AZURE_BLOB_STORAGE_CONTAINER_NAME"]
        ?? "agents-production-reference-state",
    ContainerUri = builder.Configuration["AZURE_BLOB_STORAGE_CONTAINER_URI"] is string raw
        ? new System.Uri(raw)
        : null,
};
storageOptions.Validate();

builder.Services.AddSingleton<IStorage>(
    AgentStorageFactory.Create(storageOptions, new DefaultAzureCredential()));

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "ProductionReference is running.");

app.Run();

// Required for WebApplicationFactory<Program> in tests.
public partial class Program { }

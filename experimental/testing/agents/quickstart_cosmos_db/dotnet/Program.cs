// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using QuickStart;
using Azure.Identity;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.CosmosDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<MyAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.

var cosmosDbStorageOptions = new CosmosDbPartitionedStorageOptions()
{
    DatabaseId = builder.Configuration["Storage:CosmosDbDatabaseId"],
    ContainerId = builder.Configuration["Storage:CosmosDbContainerId"],
    CosmosDbEndpoint = builder.Configuration["Storage:CosmosDbEndpoint"],
    TokenCredential = new DefaultAzureCredential()
};

builder.Services.AddSingleton<IStorage>(
    new CosmosDbPartitionedStorage(
        cosmosDbStorageOptions
    )
);

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

app.Urls.Add($"http://localhost:3978");

app.Run();

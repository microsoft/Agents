// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using PersistentState;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<MyAgent>();

// Register IStorage backed by Azure Blob Storage so that conversation state
// persists across Agent restarts and scales correctly in a cluster.
//
// Set AZURE_BLOB_STORAGE_CONNECTION_STRING in environment or user secrets.
// Set AZURE_BLOB_STORAGE_CONTAINER_NAME to override the default container name.
string connectionString = builder.Configuration["AZURE_BLOB_STORAGE_CONNECTION_STRING"]
    ?? throw new InvalidOperationException(
        "AZURE_BLOB_STORAGE_CONNECTION_STRING is required. " +
        "Set it in environment variables or user secrets.");

string containerName = builder.Configuration["AZURE_BLOB_STORAGE_CONTAINER_NAME"]
    ?? "agents-persistent-state";

builder.Services.AddSingleton<IStorage>(new BlobsStorage(connectionString, containerName));

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
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

app.Run();

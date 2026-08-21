// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MultiAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgentDefaults()
    .AddAgent<Agent1>()
    .AddAgent<Agent2>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

app.Run();

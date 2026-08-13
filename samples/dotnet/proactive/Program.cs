// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Proactive;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgentDefaults()
    .AddAgent<ProactiveAgent>()
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

// Map the endpoints for proactive messages.  This is required to receive external Http
// requests for AgentApplication.Proactive at /proactive.
// This is using ContinueConversationAttributes in the ProactiveAgent to map /proactive/continue,
// /proactive/continue/ext.
app.MapAgentProactiveEndpoints<ProactiveAgent>(requireAuth: !app.Environment.IsDevelopment());

app.Run();

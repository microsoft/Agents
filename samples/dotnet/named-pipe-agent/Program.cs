// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NamedPipeAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<MyAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the named-pipe transport for the DirectLine App Service extension (DirectLineFlex).
// The pipe is a trusted channel — the sidecar handles external authentication, so this
// pipe-only agent does NOT need JWT token validation or any HTTP endpoint mapping.
//
// These calls (used by the HTTP-based quickstart sample) are intentionally omitted:
//   builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
//   app.UseAuthentication();
//   app.UseAuthorization();
//   app.MapAgentRootEndpoint();
//   app.MapAgentApplicationEndpoints();
builder.AddAgentNamedPipeTransport();

WebApplication app = builder.Build();

app.Run();

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CopilotStudioEchoSkill;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<EchoSkill>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

// Required for providing the Agent manifest.
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthorization();
var root = app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
}).RequireAuthorization();
    
if (app.Environment.IsDevelopment())
{
    root.AllowAnonymous();
    app.UseDeveloperExceptionPage();
    app.MapGet("/", () => "Microsoft Agents SDK Sample");
}

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");

app.Run();
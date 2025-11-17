// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GenesysHandoff;
using GenesysHandoff.Genesys;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<GenesysHandoffAgent>();

// Register GenesysService as a singleton.
GenesysService? genesysService = null;
builder.Services.AddSingleton(sp =>
{
    var settings = new GenesysConnectionSetting(builder.Configuration.GetSection("Genesys"));
    genesysService = new GenesysService(settings, sp.GetService<IHttpClientFactory>()!, sp.GetService<IStorage>()!);
    return genesysService;
});

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Microsoft Agents SDK Sample for Genesys Integration");

// This receives incoming messages from Azure Bot Service or other SDK Agents
var incomingRoute = app.MapPost("/api/messages", async (
    HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

// This receives outbound proactive messages from Genesys to be sent to users
var genesysOutboundRoute = app.MapPost("/api/outbound", async (HttpRequest request, HttpResponse response, IChannelAdapter channelAdapter, CancellationToken cancellationToken) =>
{
    if (genesysService == null)
    {
        response.StatusCode = StatusCodes.Status500InternalServerError;
        await response.WriteAsync("GenesysClient not initialized", cancellationToken);
        return;
    }

    await genesysService.RetrieveMessageFromGenesysAsync(request, channelAdapter, cancellationToken);
    response.StatusCode = StatusCodes.Status200OK;
    await response.WriteAsync("Proactive message sent.", cancellationToken);
});

if (!app.Environment.IsDevelopment())
{
    incomingRoute.RequireAuthorization();
}
else
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GenesysHandoff;
using GenesysHandoff.Genesys;
using GenesysHandoff.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
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

// Register application services
builder.Services.AddSingleton<CopilotClientFactory>();
builder.Services.AddSingleton<ActivityResponseProcessor>();
builder.Services.AddSingleton<ConversationStateManager>();

// Register Genesys services.
var genesysSettings = new GenesysConnectionSetting(builder.Configuration.GetSection("Genesys"));


if (string.IsNullOrWhiteSpace(genesysSettings.WebhookSignatureSecret))
{
    throw new InvalidOperationException(
        "Genesys:WebhookSignatureSecret must be configured. " +
        "The /api/outbound endpoint is anonymous and requires webhook signature validation to prevent unauthorized access.");
}

// Register settings as a shared singleton.
builder.Services.AddSingleton<IGenesysConnectionSettings>(genesysSettings);

// Register the shared token provider for Genesys Cloud authentication.
builder.Services.AddSingleton<GenesysTokenProvider>();

// GenesysMessageSender — outbound messages to Genesys.
builder.Services.AddSingleton<GenesysMessageSender>();

// GenesysWebhookHandler — inbound webhook handling.
builder.Services.AddSingleton<GenesysWebhookHandler>();

// ConversationMappingStore — shared mapping of Genesys ↔ MCS conversation IDs.
builder.Services.AddSingleton<ConversationMappingStore>();

// Conditionally register the notification service for agent disconnect detection.
if (genesysSettings.EnableNotifications)
{
    builder.Services.AddSingleton<GenesysNotificationService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<GenesysNotificationService>());
}

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<GenesysHandoffAgent>();

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

// This receives outbound proactive messages from Genesys to be sent to users
var genesysOutboundRoute = app.MapPost("/api/outbound", async (HttpRequest request, HttpResponse response, IChannelAdapter channelAdapter, GenesysWebhookHandler webhookHandler, CancellationToken cancellationToken) =>
{
    var result = await webhookHandler.HandleAsync(request, channelAdapter, cancellationToken);
    switch (result)
    {
        case WebhookResult.Unauthorized:
            response.StatusCode = StatusCodes.Status401Unauthorized;
            await response.WriteAsync("Webhook signature validation failed.", cancellationToken);
            break;
        case WebhookResult.Accepted:
            response.StatusCode = StatusCodes.Status200OK;
            await response.WriteAsync("Request accepted.", cancellationToken);
            break;
        case WebhookResult.MessageSent:
            response.StatusCode = StatusCodes.Status200OK;
            await response.WriteAsync("Message sent.", cancellationToken);
            break;
    }
}).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

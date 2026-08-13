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
using System;
using System.Net.Http;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResilientMcsHttpClient();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Register application services
builder.Services.AddSingleton<CopilotClientFactory>();
builder.Services.AddSingleton<ActivityResponseProcessor>();
builder.Services.AddSingleton<ConversationStateManager>();
builder.Services.AddSingleton<ConversationResetService>();

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

// ActivityReplyMappingStore — mapping of Relay Bot activity IDs to MCS activity IDs.
builder.Services.AddSingleton<IActivityReplyMappingStore, ActivityReplyMappingStore>();

// Conditionally register the notification service for agent disconnect detection.
if (genesysSettings.EnableNotifications)
{
    builder.Services.AddSingleton<GenesysNotificationService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<GenesysNotificationService>());
}

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgentDefaults()
    .AddAgent<GenesysHandoffAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

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

// Endpoint to reset a conversation by conversationId.
// Accepts an optional Message that will be sent to the Teams conversation before reset.
app.MapPost("/api/conversations/reset", async (ResetConversationRequest request, ConversationResetService resetService, HttpResponse response, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request?.ConversationId))
    {
        response.StatusCode = StatusCodes.Status400BadRequest;
        await response.WriteAsJsonAsync(new { error = "ConversationId is required." }, cancellationToken);
        return;
    }

    try
    {
        var success = await resetService.ResetConversationAsync(request.ConversationId, request.Message, cancellationToken);

        if (success)
        {
            response.StatusCode = StatusCodes.Status200OK;
            await response.WriteAsJsonAsync(new { message = "Conversation reset successfully.", conversationId = request.ConversationId }, cancellationToken);
        }
        else
        {
            response.StatusCode = StatusCodes.Status409Conflict;
            await response.WriteAsJsonAsync(new { error = "Cannot reset an escalated conversation.", conversationId = request.ConversationId }, cancellationToken);
        }
    }
    catch (Exception ex)
    {
        response.StatusCode = StatusCodes.Status500InternalServerError;
        await response.WriteAsJsonAsync(new { error = "Failed to reset conversation.", details = ex.Message }, cancellationToken);
    }
}).RequireAuthorization();

app.Run();

/// <summary>
/// Request model for resetting a conversation.
/// </summary>
public class ResetConversationRequest
{
    /// <summary>
    /// The MCS conversation ID to reset.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Optional message to send to the Teams conversation before the conversation is reset.
    /// </summary>
    public string? Message { get; set; }
}

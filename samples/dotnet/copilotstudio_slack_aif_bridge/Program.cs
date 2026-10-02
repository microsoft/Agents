// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Slack_MCS_Bridge;
using System;
using System.IO;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure defaults for Aspire dashboard
builder.ConfigureOtelProviders();


builder.Services.AddSingleton<IChatClient>(sp =>
{

    IConnections connections = sp.GetRequiredService<IConnections>();
    var conn = connections.GetConnection("ServiceConnection");


    var confSvc = sp.GetRequiredService<IConfiguration>();
    var endpoint = confSvc["AIServices:AzureOpenAI:Endpoint"] ?? string.Empty;
    var apiKey = confSvc["AIServices:AzureOpenAI:ApiKey"] ?? string.Empty;
    var deployment = confSvc["AIServices:AzureOpenAI:DeploymentName"] ?? string.Empty;

    AssertionHelpers.ThrowIfNullOrEmpty(endpoint, "AIServices:AzureOpenAI:Endpoint configuration is missing and required.");
    AssertionHelpers.ThrowIfNullOrEmpty(apiKey, "AIServices:AzureOpenAI:ApiKey configuration is missing and required.");
    AssertionHelpers.ThrowIfNullOrEmpty(deployment, "AIServices:AzureOpenAI:DeploymentName configuration is missing and required.");

    // Convert endpoint to Uri
    var endpointUri = new Uri(endpoint);

    // Convert apiKey to ApiKeyCredential
    var apiKeyCredential = new AzureKeyCredential(apiKey);

    // Create and return the AzureOpenAIClient's ChatClient
    return new AzureOpenAIClient(endpointUri, apiKeyCredential)
        .GetChatClient(deployment)
        .AsIChatClient()
        .AsBuilder()
        .UseFunctionInvocation()
        .UseOpenTelemetry(loggerFactory: sp.GetService<ILoggerFactory>(), sourceName: AgentTelemetry.ServiceName, configure: (cfg) =>
        {
            cfg.EnableSensitiveData = true;
        })
        .Build();
});


// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgentDefaults()
    .AddAgent<McsSlackBridge>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Post)
    {
        // Enable buffering so we can read the body without consuming it
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();

        // Reset stream position so the next middleware/controller can read it
        context.Request.Body.Position = 0;

        // Attach body to current OTEL span
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            // WARNING: Be careful with sensitive data!
            activity.AddEvent(new System.Diagnostics.ActivityEvent(
                                    "http.request.body",
                                    tags: new System.Diagnostics.ActivityTagsCollection { { "http.request.body.content", body } }
                                ));
        }
    }

    await next();
});

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

app.Run();

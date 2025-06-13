// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;
using StreamingMessageAgent;
using System;
using System.ClientModel;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddTransient<ChatClient>(sp =>
{
    return new AzureOpenAIClient(
            new Uri(builder.Configuration["AIServices:AzureOpenAI:Endpoint"]),
            new ApiKeyCredential(builder.Configuration["AIServices:AzureOpenAI:ApiKey"]))
    .GetChatClient(builder.Configuration["AIServices:AzureOpenAI:DeploymentName"]);
});

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<StreamingAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Configure the HTTP request pipeline.

WebApplication app = builder.Build();

app.MapGet("/", () => "Microsoft Agents SDK Sample");

// This receives incoming messages from Azure Bot Service or other SDK Agents
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
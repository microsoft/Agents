// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;
using StreamingMessageAgent;
using System;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<ChatClient>(sp =>
{
    return new AzureOpenAIClient(
            new Uri(builder.Configuration["AIServices:AzureOpenAI:Endpoint"]!),
            new ApiKeyCredential(builder.Configuration["AIServices:AzureOpenAI:ApiKey"]!))
    .GetChatClient(builder.Configuration["AIServices:AzureOpenAI:DeploymentName"]);
});

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgentDefaults()
    .AddAgent<StreamingAgent>()
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

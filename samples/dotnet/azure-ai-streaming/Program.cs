// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;
using AzureAIStreaming;
using System;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

string GetRequiredSetting(string environmentVariable, string configurationPath)
{
    string? value = builder.Configuration[environmentVariable] ?? builder.Configuration[configurationPath];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required configuration: {environmentVariable}");
    }

    return value;
}

string azureOpenAIEndpoint = GetRequiredSetting("AZURE_OPENAI_ENDPOINT", "AIServices:AzureOpenAI:Endpoint");
string azureOpenAIApiKey = GetRequiredSetting("AZURE_OPENAI_API_KEY", "AIServices:AzureOpenAI:ApiKey");
string azureOpenAIDeploymentName = GetRequiredSetting("AZURE_OPENAI_DEPLOYMENT_NAME", "AIServices:AzureOpenAI:DeploymentName");

builder.Services.AddTransient<ChatClient>(sp =>
{
    return new AzureOpenAIClient(
            new Uri(azureOpenAIEndpoint),
            new ApiKeyCredential(azureOpenAIApiKey))
    .GetChatClient(azureOpenAIDeploymentName);
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

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using RetrievalBot;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddHttpClient();

// Register Semantic Kernel
builder.Services.AddKernel();


// Register the AI service of your choice. AzureOpenAI and OpenAI are demonstrated...
if (builder.Configuration.GetSection("AIServices").GetValue<bool>("UseAzureOpenAI"))
{
    builder.Services.AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("DeploymentName")!,
        endpoint: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("Endpoint")!,
        apiKey: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("ApiKey")!);

        /* //Use the Azure CLI (for local) or Managed Identity (for Azure running app) to authenticate to the Azure OpenAI service
        credentials: new ChainedTokenCredential(
           new AzureCliCredential(),
           new ManagedIdentityCredential()
        ));  */
}
else
{
    builder.Services.AddOpenAIChatCompletion(
        modelId: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ModelId")!,
        apiKey: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ApiKey")!);
}

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add basic bot functionality
builder.AddAgent<Retrieval>();

builder.Services.AddSingleton<IStorage>(new MemoryStorage());
builder.Services.AddSingleton<ConversationState>();

// Configure the HTTP request pipeline.

// Add AspNet token validation
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();


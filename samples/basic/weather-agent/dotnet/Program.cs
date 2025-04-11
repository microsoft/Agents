// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using System.IO;
using System.Threading;
using WeatherBot;
using WeatherBot.Agents;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Register Semantic Kernel
builder.Services.AddKernel();

// Register the AI service of your choice. AzureOpenAI and OpenAI are demonstrated...
if (builder.Configuration.GetSection("AIServices").GetValue<bool>("UseAzureOpenAI"))
{
    builder.Services.AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("DeploymentName"),
        endpoint: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("Endpoint"),
        apiKey: builder.Configuration.GetSection("AIServices:AzureOpenAI").GetValue<string>("ApiKey"));

    //Use the Azure CLI (for local) or Managed Identity (for Azure running app) to authenticate to the Azure OpenAI service
    //credentials: new ChainedTokenCredential(
    //   new AzureCliCredential(),
    //   new ManagedIdentityCredential()
    //));
}
else
{
    builder.Services.AddOpenAIChatCompletion(
        modelId: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ModelId"),
        apiKey: builder.Configuration.GetSection("AIServices:OpenAI").GetValue<string>("ApiKey"));
}

builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.AddAgentApplicationOptions();

//builder.Services.AddTransient<WeatherForecastAgent>();

builder.AddAgent<MyAgent>();

builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");


app.Run();


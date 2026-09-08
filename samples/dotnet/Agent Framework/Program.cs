// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentFrameworkWeather;
using AgentFrameworkWeather.Agent;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Extensions.AI;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly());
builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Configure defaults for Aspire dashboard
builder.ConfigureOtelProviders();

builder.Logging.AddConsole();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the bot (which is transient) and configure AspNet token validation.
// Authorization (and therefore required auth on the mapped endpoints) is enabled
// for all environments except Development and Playground.
builder.AddAgentDefaults()
    .AddAgent<WeatherAgent>()
    .AddAgentAuthorization(
        b => b.AddAgentAspNetAuthentication(),
        forceEnable: !(builder.Environment.IsDevelopment() || builder.Environment.EnvironmentName == "Playground"));

// Register IChatClient with correct types
builder.Services.AddSingleton<IChatClient>(sp => {

    var confSvc = sp.GetRequiredService<IConfiguration>();
    var endpoint = confSvc["AIServices:AzureOpenAI:Endpoint"] ?? string.Empty;
    var apiKey = confSvc["AIServices:AzureOpenAI:ApiKey"] ?? string.Empty;
    var deployment = confSvc["AIServices:AzureOpenAI:DeploymentName"] ?? string.Empty;

    // Validate OpenWeatherAPI key. 
    var openWeatherApiKey = confSvc["OpenWeatherApiKey"] ?? string.Empty;

    AssertionHelpers.ThrowIfNullOrEmpty(endpoint, "AIServices:AzureOpenAI:Endpoint configuration is missing and required.");
    AssertionHelpers.ThrowIfNullOrEmpty(apiKey, "AIServices:AzureOpenAI:ApiKey configuration is missing and required.");
    AssertionHelpers.ThrowIfNullOrEmpty(deployment, "AIServices:AzureOpenAI:DeploymentName configuration is missing and required.");
    AssertionHelpers.ThrowIfNullOrEmpty(openWeatherApiKey, "OpenWeatherApiKey configuration is missing and required.");

    // Convert endpoint to Uri
    var endpointUri = new Uri(endpoint);

    // Convert apiKey to ApiKeyCredential
    var apiKeyCredential = new AzureKeyCredential(apiKey);

    // Create and return the AzureOpenAIClient's ChatClient
    return new AzureOpenAIClient(endpointUri, apiKeyCredential).GetChatClient(deployment).AsIChatClient(); 
});

// Uncomment to add transcript logging middleware to log all conversations to files
builder.Services.AddSingleton<Microsoft.Agents.Builder.IMiddleware[]>([new TranscriptLoggerMiddleware(new FileTranscriptLogger())]);

var app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline
// (with routing enabled so the controllers below can be mapped).
app.UseAgents(useRouting: true);

// Map the default agent endpoints: GET "/" and the agent message endpoints.
// Authorization is required automatically when AddAgentAuthorization enabled it above.
app.MapDefaultAgentEndpoints();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Playground")
{
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();
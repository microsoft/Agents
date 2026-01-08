// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CopilotStudioClientSample;
using Microsoft.Agents.CopilotStudio.Client;

// Setup the Direct To Engine client example.

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Get the configuration settings for both copilots from the appsettings.json file.
SampleConnectionSettings settings1 = new SampleConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings1"));
SampleConnectionSettings settings2 = new SampleConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings2"));

// Create http clients for both copilots
builder.Services.AddHttpClient("copilot1").ConfigurePrimaryHttpMessageHandler(() =>
{
    if (settings1.UseS2SConnection)
    {
        return new AddTokenHandlerS2S(settings1);
    }
    else
    {
        return new AddTokenHandler(settings1);
    }
});

builder.Services.AddHttpClient("copilot2").ConfigurePrimaryHttpMessageHandler(() =>
{
    if (settings2.UseS2SConnection)
    {
        return new AddTokenHandlerS2S(settings2);
    }
    else
    {
        return new AddTokenHandler(settings2);
    }
});

// add Settings and instances of both Copilot Clients to the Current services.  
builder.Services
    .AddSingleton(settings1)
    .AddSingleton(settings2)
    .AddKeyedTransient<CopilotClient>("copilot1", (s, key) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
        return new CopilotClient(settings1, s.GetRequiredService<IHttpClientFactory>(), logger, "copilot1");
    })
    .AddKeyedTransient<CopilotClient>("copilot2", (s, key) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
        return new CopilotClient(settings2, s.GetRequiredService<IHttpClientFactory>(), logger, "copilot2");
    })
    .AddHostedService<DualCopilotChatService>();
IHost host = builder.Build();
host.Run();

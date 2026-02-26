// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using DispatcherAgent;
using DispatcherAgent.Interfaces;
using DispatcherAgent.Model;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add AspNet Authentication suitable for token validation for a Bot Service bot (ABS- or SMBA)
builder.Services.AddBotAspNetAuthentication(builder.Configuration);
builder.Services.AddAzureOpenAIChatCompletion("gpt-4o", builder.Configuration["AOAI_ENDPOINT"]!.ToString(), builder.Configuration["AOAI_APIKEY"]!.ToString());


BotSetup(builder);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Microsoft Copilot SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();

static void BotSetup(IHostApplicationBuilder builder)
{
    ArgumentNullException.ThrowIfNull(builder);

    builder.Services.AddSingleton<IMCSAgents>(o => new MCSAgents(o, builder.Configuration, "McsOBOConnection", "MCS01"));

    // Add default bot MsalAuth support
    builder.Services.AddDefaultMsalAuth(builder.Configuration);

    // Add Connections object to access configured token connections.
    builder.Services.AddSingleton<IConnections, ConfigurationConnections>();

    // Add factory for ConnectorClient and UserTokenClient creation
    builder.Services.AddSingleton<IChannelServiceClientFactory, RestChannelServiceClientFactory>();

    // Add IStorage for turn state persistence
    builder.Services.AddSingleton<IStorage, MemoryStorage>();

    // Add the BotAdapter
    builder.Services.AddCloudAdapter();

    // Add the Bot
    builder.Services.AddTransient<IBot, DispatcherAgent.DispatcherBot>();
}

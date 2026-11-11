// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AgentTargetedMessages;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<AgentTargetedMessagesAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<ReminderService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<ReminderService>());

WebApplication app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();

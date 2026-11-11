// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using BotMessageExtensions;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<BotMessageExtensionsAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

builder.Services.AddHttpClient();

WebApplication app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();

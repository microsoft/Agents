// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using BotAiMessages;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<BotAiMessagesAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();

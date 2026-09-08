// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EntraAgentIdSidecar;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<EchoAgent>()
    .AddAgentAuthorization(
        authorization => authorization.AddAgentAspNetAuthentication(),
        forceEnable: true);

builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();

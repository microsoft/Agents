// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetrievalAgent;
using RetrievalAgent.Services;
using System;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RetrievalOptions>()
    .Bind(builder.Configuration.GetSection(RetrievalOptions.SectionName))
    .Validate(options => RetrievalOptions.IsValidSiteUrl(options.SharePointSiteUrl), "Retrieval:SharePointSiteUrl must be an absolute HTTPS SharePoint site URL.")
    .Validate(options => options.MaximumNumberOfResults is >= 1 and <= 25, "Retrieval:MaximumNumberOfResults must be from 1 through 25.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IBuildRetrievalService, BuildRetrievalService>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
});
builder.Services.AddSingleton<IBuildGenieMessageRoute, BuildGenieMessageRoute>();

builder.AddAgentDefaults()
    .AddAgent<Retrieval>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();
app.UseAgents();
app.MapDefaultAgentEndpoints();
app.Run();

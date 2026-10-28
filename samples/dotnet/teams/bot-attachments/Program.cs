// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Teams.Samples.BotAttachments;
using Microsoft.Teams.Samples.BotAttachments.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<BotAttachmentsAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<PendingUploadStore>();
builder.Services.AddSingleton<FileUploadQueue>();
builder.Services.AddSingleton<IFileUploadQueue>(serviceProvider => serviceProvider.GetRequiredService<FileUploadQueue>());
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<FileUploadQueue>());

WebApplication app = builder.Build();

app.UseAgents();
app.MapDefaultAgentEndpoints();

app.Run();

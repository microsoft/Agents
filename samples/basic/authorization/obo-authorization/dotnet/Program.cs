// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent(sp =>
{
    const string MCSConversationPropertyName = "MCSConversationId";

    var app = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());

    CopilotClient GetClient(AgentApplication app, ITurnContext turnContext)
    {
        var settings = new ConnectionSettings(builder.Configuration.GetSection("CopilotStudioAgent"));
        string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

        return new CopilotClient(
            settings,
            sp.GetService<IHttpClientFactory>(),
            tokenProviderFunction: async (s) =>
            {
                // In this sample, the Azure Bot OAuth Connection is configured to return an 
                // exchangeable token, that can be exchange for different scopes.  This can be
                // done multiple times using different scopes.
                return await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
            },
            NullLogger.Instance,
            "mcs");
    }

    app.OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
    {
        // Force a user signout to reset the user state
        // This is needed to reset the token in Azure Bot Services if needed. 
        // Typically this wouldn't be need in a production Agent.  Made available to assist it starting from scratch.
        await app.UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
    }, rank: RouteRank.First);

    // Since Auto SignIn is enabled, by the time this is called the token is already available via UserAuthorization.GetTurnTokenAsync or
    // UserAuthorization.ExchangeTurnTokenAsync.
    // NOTE:  This is a slightly unusual way to handle incoming Activities (but perfectly) valid.  For this sample,
    // we just want to proxy messages to/from a Copilot Studio Agent.
    app.OnActivity((turnContext, cancellationToken) => Task.FromResult(true), async (turnContext, turnState, cancellationToken) =>
    {
        
        var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
        var cpsClient = GetClient(app, turnContext);

        if (string.IsNullOrEmpty(mcsConversationId))
        {
            // Regardless of the Activity  Type, start the conversation.
            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);

                    // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                    turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                }
            }
        }
        else if (turnContext.Activity.IsType(ActivityTypes.Message))
        {
            // Send the Copilot Studio Agent whatever the sent and send the responses back.
            await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                }
            }
        }
    }, autoSignInHandlers: ["mcs"]);

    // Called when the OAuth flow fails
    app.UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error.Message}", cancellationToken: cancellationToken);
    });

    return app;
});


// Configure the HTTP request pipeline.

WebApplication app = builder.Build();

app.MapGet("/", () => "Microsoft Agents SDK Sample");

// This receives incoming messages from Azure Bot Service or other SDK Agents
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GenesysHandoff;
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

GenesysClient? globalGenesysClient = null;

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
    const string IsEscalatedPropertyName = "IsEscalated";

    var app = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());

    CopilotClient GetClient(AgentApplication app, ITurnContext turnContext)
    {
        var settings = new ConnectionSettings(builder.Configuration.GetSection("CopilotStudioAgent"));
        string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

        return new CopilotClient(
            settings,
            sp.GetService<IHttpClientFactory>()!,
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

    GenesysClient GetGenesysClient(AgentApplication app)
    {
        if (globalGenesysClient == null)
        {
            var settings = new GenesysConnectionSetting(builder.Configuration.GetSection("Genesys"));
            globalGenesysClient = new GenesysClient(settings, sp.GetService<IHttpClientFactory>()!, sp.GetService<IStorage>()!);
        }
        return globalGenesysClient;
    }

    app.OnMessage("-reset", async (turnContext, turnState, cancellationToken) =>
    {
        // Reset the conversation by clearing the conversation state.
        turnState.Conversation.DeleteValue(MCSConversationPropertyName);
        turnState.Conversation.DeleteValue(IsEscalatedPropertyName);
        await turnContext.SendActivityAsync("The conversation has been reset.", cancellationToken: cancellationToken);
    }, rank: RouteRank.First);

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
            var isEscalated = turnState.Conversation.GetValue<bool>(IsEscalatedPropertyName);
            // If already escalated, just send the message to Genesys.
            if (isEscalated)
            {
                var genesysClient = GetGenesysClient(app);
                await genesysClient.SendMessageToGenesysAsync(turnContext.Activity, mcsConversationId, cancellationToken);
            }
            else
            {
                //Send the Copilot Studio Agent whatever the sent and send the responses back.
                await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                    }
                    // If the Agent sends an Event activity named "GenesysHandoff", then escalate to Genesys.
                    if (activity.IsType(ActivityTypes.Event) && activity.Name.Equals("GenesysHandoff"))
                    {
                        turnState.Conversation.SetValue(IsEscalatedPropertyName, true);
                        var genesysClient = GetGenesysClient(app);
                        var summarizationActivity = turnContext.Activity.GetConversationReference().GetContinuationActivity();
                        summarizationActivity.Text = activity.Value?.ToString() ?? "The chat is being escalated to a human agent.";
                        await genesysClient.SendMessageToGenesysAsync(summarizationActivity, mcsConversationId, cancellationToken);
                    }
                }
            }
        }
    }, autoSignInHandlers: ["mcs"]);

    // Called when the OAuth flow fails
    app.UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
    });

    return app;
});


// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Microsoft Agents SDK Sample for Genesys Integration");

// This receives incoming messages from Azure Bot Service or other SDK Agents
var incomingRoute = app.MapPost("/api/messages", async (
    HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

// This receives outgoing messages from Genesys and relays them to the user via the Agent.
var genesysOutboundRoute = app.MapPost("/api/outbound", async (HttpRequest request, HttpResponse response, IChannelAdapter channelAdapter, CancellationToken cancellationToken) =>
{
    if (globalGenesysClient == null)
    {
        response.StatusCode = StatusCodes.Status500InternalServerError;
        await response.WriteAsync("GenesysClient not initialized", cancellationToken);
        return;
    }

    await globalGenesysClient.RetrieveMessageFromGenesysAsync(request, channelAdapter, cancellationToken);
    response.StatusCode = StatusCodes.Status200OK;
    await response.WriteAsync("Proactive message sent.", cancellationToken);
});

if (!app.Environment.IsDevelopment())
{
    incomingRoute.RequireAuthorization();
}
else
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

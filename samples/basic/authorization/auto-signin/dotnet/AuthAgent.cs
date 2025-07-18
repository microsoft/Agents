// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AutoSignIn;
public class AuthAgent : AgentApplication
{
    /// <summary>
    /// Default Sign In Name
    /// </summary>
    private readonly string _defaultDisplayName = "Unknown User";

    /// <summary>
    /// Describes the agent registration for the Authorization Agent
    /// This agent will handle the sign-in and sign-out OAuth processes for a user.
    /// </summary>
    /// <param name="options">AgentApplication Configuration objects to configure and setup the Agent Application</param>
    public AuthAgent(AgentApplicationOptions options) : base(options)
    {
        // During setup of the Agent Application, Register Event Handlers for the Agent. 
        // For this example we will register a welcome message for the user when they join the conversation, then configure sign-in and sign-out commands.
        // Additionally, we will add events to handle notifications of sign-in success and failure,  these notifications will report the local log instead of back to the calling agent.

        // This constructor should only register events and setup state as it will be called for each request. 
        
        // When a conversation update event is triggered. 
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Demonstrates the use of Per-Route Auto sign-in.  This will automatically get a token using the indicated OAuth handler for this message route.
        // This Route will automatically get a token using the "me" UserAuthorization.Handler in config.
        OnMessage("-me", OnMe, autoSignInHandlers: ["me"]);

        // Handles the user sending a SignOut command using the specific keywords '-signout'
        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            // Force a user signout to reset the user state
            // This is needed to reset the token in Azure Bot Services if needed. 
            // Typically this wouldn't be need in a production Agent.  Made available to assist it starting from scratch.
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, "me", cancellationToken: cancellationToken);
            await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
        }, rank: RouteRank.Last);

        // Registers a general event handler that will pick up any message activity that is not covered by the previous events handlers. 
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        // The UserAuthorization Class provides methods and properties to manage and access user authorization tokens
        // You can use this class to interact with the UserAuthorization process, including signing in and signing out users, accessing tokens, and handling authorization events.

        // Register Events for SignIn Failure on the UserAuthorization class to notify the Agent in the event of an OAuth failure.
        // For a production Agent, this would typically be used to provide instructions to the end-user.  For example, call/email or
        // handoff to a live person (depending on Agent capabilities).
        UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);
    }

    /// <summary>
    /// This method is called to handle the conversation update event when a new member is added to or removed from the conversation.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // In this example, we will send a welcome message to the user when they join the conversation.
        // We do this by iterating over the incoming activity members added to the conversation and checking if the member is not the agent itself.
        // Then a greeting notice is provided to each new member of the conversation.
        
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                string displayName = await GetDisplayName(turnContext);
                StringBuilder sb = new();
                sb.AppendLine($"Welcome to the AutoSignIn Example, **{displayName}**!");
                sb.AppendLine("This Agent automatically signs you in when you first connect.");
                sb.AppendLine("You can use the following commands to interact with the agent:");
                sb.AppendLine("**-me**: Displays detailed information using a different OAuth Token.");
                sb.AppendLine("**-signout**: Sign out of the agent and force it to reset the login flow on next message.");
                if (displayName.Equals(_defaultDisplayName))
                {
                    sb.AppendLine("**WARNING: We were unable to get your display name with the current access token.. please use the -signout command before proceeding**");
                }
                sb.AppendLine("");
                sb.AppendLine("Type anything else to see the agent echo back your message.");
                await turnContext.SendActivityAsync(MessageFactory.Text(sb.ToString()), cancellationToken);
                sb.Clear();
            }
        }
    }

    /// Handles -me, using a different OAuthConnection to show Per-Route OAuth. 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task OnMe(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // If successful, the user will be the token will be available from the UserAuthorization.GetTurnTokenAsync(turnContext, DefaultHandlerName) call. 
        // If not successful, this handler won't be reached.  Instead, OnUserSignInFailure handler would have been called. 

        // For this sample, two OAuth Connections are setup to demonstrate multiple OAuth Connections and Auto SignIn handling and routing.
        // For ease of setup, both connections are using the same App Reg for API Permissions.  In a real Agent, these would be using different
        // App Registrations with different permissions. In this cases, we are using two different tokens to access external services.

        var displayName = await GetDisplayName(turnContext);
        var graphInfo = await GetGraphInfo(turnContext, "me");

        // Just to verify "auto" handler setup.  This wouldn't be needed in a production Agent and here just to verify sample setup.
        if (displayName.Equals(_defaultDisplayName) || graphInfo == null)
        {
            await turnContext.SendActivityAsync($"Failed to get information from handlers '{UserAuthorization.DefaultHandlerName}' and/or 'me'. \nDid you update the scope correctly in Azure bot Service?. If so type in -signout to force signout the current user", cancellationToken: cancellationToken);
            return;
        }

        // Just to verify we in fact have two different tokens.  This wouldn't be needed in a production Agent and here just to verify sample setup.
        if (await UserAuthorization.GetTurnTokenAsync(turnContext, UserAuthorization.DefaultHandlerName, cancellationToken: cancellationToken) == await UserAuthorization.GetTurnTokenAsync(turnContext, "me", cancellationToken))
        {
            await turnContext.SendActivityAsync($"It would seem '{UserAuthorization.DefaultHandlerName}' and 'me' are using the same OAuth Connection", cancellationToken: cancellationToken);
            return;
        }

        var meInfo = $"Name: {displayName}\r\nJob Title: {graphInfo["jobTitle"]!.GetValue<string>()}\r\nEmail: {graphInfo["mail"]!.GetValue<string>()}";
        await turnContext.SendActivityAsync(meInfo, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Handles general message loop. 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns></returns>
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // When Auto Sign in is properly configured, the user will be automatically signed in when they first connect to the agent using the default
        // handler chosen in the UserAuthorization configuration.
        // IMPORTANT: The ReadMe associated with this sample, instructs you on configuring the Azure Bot Service Registration with the scopes to allow
        // you to read your own information from Graph.  you must have completed that for this sample to work correctly. 

        // If successful, the user will be the token will be available from the UserAuthorization.GetTurnTokenAsync(turnContext, DefaultHandlerName) call. 
        // If not successful, this handler won't be reached.  Instead, OnUserSignInFailure handler would have been called. 
        
        // We have the access token, now try to get your user name from graph. 
        string displayName = await GetDisplayName(turnContext);
        if (displayName.Equals(_defaultDisplayName))
        {
            // Handle error response from Graph API
            await turnContext.SendActivityAsync($"Failed to get user information from Graph API \nDid you update the scope correctly in Azure bot Service?. If so type in -signout to force signout the current user", cancellationToken: cancellationToken);
            return;
        }

        // Now Echo back what was said with your display name. 
        await turnContext.SendActivityAsync($"**{displayName} said:** {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// This method is called when the sign-in process fails with an error indicating why . 
    /// </summary>
    /// <param name="turnContext"></param>
    /// <param name="turnState"></param>
    /// <param name="handlerName"></param>
    /// <param name="response"></param>
    /// <param name="initiatingActivity"></param>
    /// <param name="cancellationToken"></param>
    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        // Raise a notification to the user that the sign-in process failed.  In a production Agent, this would be used
        // to display alternative ways to get help, or in some cases transfer to a live agent.
        await turnContext.SendActivityAsync($"Sign In: Failed to login to '{handlerName}': {response.Cause}/{response.Error!.Message}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the display name of the user from the Graph API using the access token.
    /// </summary>
    private async Task<string> GetDisplayName(ITurnContext turnContext)
    {
        string displayName = _defaultDisplayName;
        var graphInfo = await GetGraphInfo(turnContext, UserAuthorization.DefaultHandlerName);
        if (graphInfo != null)
        {
            displayName = graphInfo!["displayName"]!.GetValue<string>();
        }
        return displayName;
    }

    private async Task<JsonNode> GetGraphInfo(ITurnContext turnContext, string handleName)
    {
        string accessToken = await UserAuthorization.GetTurnTokenAsync(turnContext, handleName);
        string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await client.GetAsync(graphApiUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonNode.Parse(content)!;
            }
        }
        catch (Exception ex)
        {
            // Handle error response from Graph API
            System.Diagnostics.Trace.WriteLine($"Error getting display name: {ex.Message}");
        }
        return null!;
    }
}

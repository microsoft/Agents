using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Authentication;
using Microsoft.Identity.Client;
using System.Reflection;
using DispatcherAgent.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;

namespace DispatcherAgent.Utils
{
    public static class Utilities
    {
        private static ConcurrentDictionary<string, IConfidentialClientApplication> _clientApps = new ConcurrentDictionary<string, IConfidentialClientApplication>();

        public static string GetFlowStateStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"{channelId}/conversations/{conversationId}/flowState".ToLower();
        }

        public static string GetConversationLinkStorageKey(ITurnContext turnContext, string? remoteName)
        {
            if (string.IsNullOrEmpty(remoteName)) throw new ArgumentNullException(nameof(remoteName));
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            var conversationLink = $"{channelId}/conversations/{conversationId}/{remoteName}/conversationLink".ToLower();
            WriteMemoryLinks(conversationLink);
            return conversationLink;
        }

        public static async Task<AuthenticationResult?> LoginFlowHandler(ITurnContext<IMessageActivity> turnContext, IStorage storage, IAccessTokenProvider? connection, string absOAuthConnectionName, string Url, string connectionKey, CancellationToken cancellationToken)
        {
            PerfTelemetryStore.AddTelemetry("LoginFlowHandler", new PerfTelemetry { ScenarioName = "Start", Duration = TimeSpan.Zero });
            Stopwatch loginFlowSW = Stopwatch.StartNew();
            try
            {
                OAuthFlow _flow = new OAuthFlow("Sign In", "Custom SignIn Message", absOAuthConnectionName, 30000, true);

                if (string.Equals("logout", turnContext.Activity.Text, StringComparison.OrdinalIgnoreCase))
                {
                    await _flow.SignOutUserAsync(turnContext, cancellationToken);
                    await turnContext.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
                    return null;
                }


                TokenResponse? tokenResponse = null;

                // Read flow state for this conversation
                var stateKey = GetFlowStateStorageKey(turnContext);
                var items = await storage.ReadAsync([stateKey], cancellationToken);
                OAuthFlowState state = items.TryGetValue(stateKey, out var value) ? (OAuthFlowState)value : new OAuthFlowState();

                if (!state.FlowStarted)
                {
                    tokenResponse = await _flow.BeginFlowAsync(turnContext, Microsoft.Agents.Core.Models.Activity.CreateMessageActivity(), cancellationToken);
                    PerfTelemetryStore.AddTelemetry("LoginFlowHandler", new PerfTelemetry { ScenarioName = "GetUserAssertionToken From ABS", Duration = loginFlowSW.Elapsed });
                    // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
                    if (tokenResponse == null)
                    {
                        var expires = DateTime.UtcNow.AddMilliseconds(_flow.Timeout ?? TimeSpan.FromMinutes(15).TotalMilliseconds);

                        state.FlowStarted = true;
                        state.FlowExpires = expires;
                    }

                }
                else
                {
                    try
                    {
                        tokenResponse = await _flow.ContinueFlowAsync(turnContext, state.FlowExpires, cancellationToken);
                        PerfTelemetryStore.AddTelemetry("LoginFlowHandler", new PerfTelemetry { ScenarioName = "ContunueUserAssertionToken From ABS", Duration = loginFlowSW.Elapsed });
                        if (tokenResponse != null)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                        }
                        else
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
                        }
                    }
                    catch (TimeoutException)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("You did not respond in time.  Please try again."), cancellationToken);
                    }

                    state.FlowStarted = false;
                }

                // Store flow state
                items[stateKey] = state;
                await storage.WriteAsync(items, cancellationToken);


                if (tokenResponse != null)
                {
                    if (connection != null && connection is MsalAuth authLib)
                    {
                        IConfidentialClientApplication? clientApp = null;
                        if (_clientApps.ContainsKey(connectionKey))
                        {
                            _clientApps.TryRemove(connectionKey, out clientApp);
                        }

                        if (clientApp == null)
                        {
                            var method = authLib.GetType().GetMethod("CreateClientApplication", BindingFlags.NonPublic | BindingFlags.Instance);
                            var holderApp = method?.Invoke(authLib, null);
                            if (holderApp != null && holderApp is IConfidentialClientApplication confAppNew)
                            {
                                clientApp = confAppNew;
                                _clientApps.TryAdd(connectionKey, confAppNew);
                            }
                        }
                        // invoke the private CreateClientApplication method on authLib
                        if (clientApp != null && clientApp is IConfidentialClientApplication confApp)
                        {
                            var authenticationResult = await confApp.AcquireTokenOnBehalfOf(new string[] { Url }, new UserAssertion(tokenResponse.Token)).ExecuteAsync();
                            PerfTelemetryStore.AddTelemetry("LoginFlowHandler", new PerfTelemetry { ScenarioName = "AcquireUserOBOToken", Duration = loginFlowSW.Elapsed });
                            return authenticationResult;
                        }
                    }

                    /*

                    var userTokenClient = turnContext.TurnState.Get<IUserTokenClient>();
                    var token = await userTokenClient.ExchangeTokenAsync(
                        turnContext.Activity.From.Id,
                        turnContext.Activity.ChannelId,
                        config["ConnectionName"], 
                        new TokenExchangeRequest(token:tokenResponse.Token), cancellationToken);

                    await turnContext.SendActivityAsync(MessageFactory.Text($"Here is your token {token.Token}"), cancellationToken);

                    */
                }
                return null;
            }
            finally
            {
                loginFlowSW.Stop();
                PerfTelemetryStore.AddTelemetry("LoginFlowHandler", new PerfTelemetry { ScenarioName = "Complete", Duration = loginFlowSW.Elapsed });
                PerfTelemetryStore.WriteTelemetry();
            }
        }

        public static void WriteConverationLinks(string text)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void WriteMemoryLinks(string text)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("(STOREACC)");
            Console.ResetColor();
            Console.Write($"{text}\n");
            Console.ResetColor();
        }

        public static void WriteCPSLinks(string text)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("(MCS)");
            Console.ResetColor();
            Console.Write($"{text}\n");
            Console.ResetColor();
        }

    }
}

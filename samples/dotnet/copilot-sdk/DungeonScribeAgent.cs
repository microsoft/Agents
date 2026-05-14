// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GitHub.Copilot.SDK;
using CopilotSdk.Tools;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SignInResponse = Microsoft.Agents.Builder.UserAuth.SignInResponse;

namespace CopilotSdk;

public class DungeonScribeAgent : AgentApplication
{
    private static CopilotClient? _copilotClient;
    private static readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _sessionSemaphore = new(1, 1);
    // Reuse Copilot sessions per user+conversation for multi-turn context
    private static readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();

    private const string DungeonScribePersona = @"You are the Dungeon Scribe, a dramatic and theatrical fantasy narrator who serves as the party's faithful record-keeper. You speak with flair and gravitas, using vivid fantasy language.

When rolling dice, always use the roll_dice tool — never simulate rolls yourself.
When managing inventory, always use the manage_inventory tool.

Keep responses concise but flavorful. Use emoji sparingly for emphasis (🎲⚔️🗡️🐉🏰📦🎒🗺️).";

    public DungeonScribeAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Sign-out command to reset the GitHub OAuth token
        OnMessage("-signout", async (turnContext, turnState, cancellationToken) =>
        {
            await UserAuthorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);
            // Remove cached sessions for this user so a fresh token is used on next sign-in
            string userId = turnContext.Activity.From?.Id ?? "anonymous";
            foreach (var key in _sessions.Keys.Where(k => k.StartsWith($"{userId}:")).ToList())
            {
                _sessions.TryRemove(key, out _);
            }
            await turnContext.SendActivityAsync("📜 *The Scribe closes the scroll…* You have been signed out. Send any message to sign in again.", cancellationToken: cancellationToken);
        });

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);

        UserAuthorization.OnUserSignInFailure(OnUserSignInFailureAsync);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text(
                        "⚔️ *The Dungeon Scribe unfurls a weathered scroll and dips quill in ink...*\n\n" +
                        "Hail, brave adventurer! I am the **Dungeon Scribe**, keeper of quests and chronicler of legends.\n\n" +
                        "I can:\n" +
                        "- 🎲 **Roll dice** — just say something like 'roll 2d6+3'\n" +
                        "- 📦 **Manage inventory** — 'add Sword of Truth to inventory'\n" +
                        "- 🗺️ **Narrate your adventures** — describe scenes, locations, encounters\n\n" +
                        "What tale shall we weave today?"
                    ),
                    cancellationToken
                );
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string? userText = turnContext.Activity.Text;
        if (string.IsNullOrWhiteSpace(userText))
        {
            return;
        }

        // Start a Streaming Process to let clients that support streaming know that we are processing the request.
        await turnContext.StreamingResponse.QueueInformativeUpdateAsync("The gods confer… stand fast a moment.").ConfigureAwait(false);

        // Get the user's GitHub OAuth token (acquired by AutoSignIn via Azure Bot OAuth Connection)
        string githubToken = await UserAuthorization.GetTurnTokenAsync(turnContext, UserAuthorization.DefaultHandlerName);

        // Key sessions by user + conversation so each user gets their own Copilot identity
        string userId = turnContext.Activity.From?.Id ?? "anonymous";
        string conversationId = turnContext.Activity.Conversation?.Id ?? "default";
        string sessionKey = $"{userId}:{conversationId}";

        try
        {
            CopilotClient client = await GetCopilotClientAsync(cancellationToken);
            CopilotSession session = await GetOrCreateSessionAsync(client, sessionKey, githubToken, cancellationToken);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool anyDeltas = false;

            using var subscription = session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent deltaEvent:
                        // Incremental token-by-token streaming — preferred path
                        if (deltaEvent.Data?.DeltaContent is string delta && delta.Length > 0)
                        {
                            anyDeltas = true;
                            turnContext.StreamingResponse.QueueTextChunk(delta);
                        }
                        break;
                    case SessionIdleEvent:
                        tcs.TrySetResult();
                        break;
                    case SessionErrorEvent errorEvent:
                        tcs.TrySetException(new InvalidOperationException(
                            $"Session error: {errorEvent.Data?.Message ?? "unknown error"}"));
                        break;
                }
            });

            await session.SendAsync(new MessageOptions { Prompt = userText }, cancellationToken: cancellationToken);
            await tcs.Task.WaitAsync(cancellationToken);

            if (!anyDeltas)
            {
                await turnContext.SendActivityAsync(
                    "📜 *The Scribe's quill hesitates...* I'm sorry, I couldn't conjure a response. Try again?",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            // Discard the cached session on error so it gets recreated next turn
            _sessions.TryRemove(sessionKey, out _);
            Console.Error.WriteLine($"Copilot SDK error: {ex}");
            await turnContext.SendActivityAsync(
                "⚠️ *A magical disturbance disrupts the Scribe's work.* " +
                "Verify that you signed in with a GitHub account that has an active Copilot subscription, then try again.",
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false); // End the streaming response
        }
    }


    private static async Task<CopilotSession> GetOrCreateSessionAsync(CopilotClient client, string sessionKey, string githubToken, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(sessionKey, out CopilotSession? existing))
        {
            return existing;
        }

        await _sessionSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_sessions.TryGetValue(sessionKey, out existing))
            {
                return existing;
            }

            string model = Environment.GetEnvironmentVariable("COPILOT_MODEL") ?? "gpt-4.1";

            CopilotSession session = await client.CreateSessionAsync(new SessionConfig
            {
                GitHubToken = githubToken,
                Model = model,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                Tools = [DiceRoller.CreateTool(), InventoryManager.CreateTool(sessionKey)],
                Streaming = true,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = DungeonScribePersona,
                },
            }, cancellationToken);

            _sessions.TryAdd(sessionKey, session);
            return session;
        }
        finally
        {
            _sessionSemaphore.Release();
        }
    }


    private static async Task<CopilotClient> GetCopilotClientAsync(CancellationToken cancellationToken)
    {
        if (_copilotClient != null)
        {
            return _copilotClient;
        }

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_copilotClient == null)
            {
                CopilotClient client = new();
                await client.StartAsync(cancellationToken);
                _copilotClient = client;
            }
        }
        finally
        {
            _initSemaphore.Release();
        }

        return _copilotClient;
    }

    private async Task OnUserSignInFailureAsync(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(
            $"⚠️ *The Scribe cannot verify your identity.* Sign-in failed for '{handlerName}': {response.Cause}/{response.Error?.Message}",
            cancellationToken: cancellationToken
        );
    }
}

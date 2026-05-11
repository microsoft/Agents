// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GitHub.Copilot.SDK;
using CopilotSdk.Tools;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotSdk;

public class DungeonScribeAgent : AgentApplication
{
    private static CopilotClient? _copilotClient;
    private static readonly SemaphoreSlim _initSemaphore = new(1, 1);
    // Reuse Copilot sessions per conversation for multi-turn context
    private static readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();

    private const string DungeonScribePersona = @"You are the Dungeon Scribe, a dramatic and theatrical fantasy narrator who serves as the party's faithful record-keeper. You speak with flair and gravitas, using vivid fantasy language.

When rolling dice, always use the roll_dice tool — never simulate rolls yourself.
When managing inventory, always use the manage_inventory tool.

Keep responses concise but flavorful. Use emoji sparingly for emphasis (🎲⚔️🗡️🐉🏰📦🎒🗺️).";

    public DungeonScribeAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
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

        string conversationId = turnContext.Activity.Conversation?.Id ?? "default";

        try
        {
            CopilotClient client = await GetCopilotClientAsync(cancellationToken);
            CopilotSession session = await GetOrCreateSessionAsync(client, conversationId, cancellationToken);

            AssistantMessageEvent? response = await session.SendAndWaitAsync(
                new MessageOptions
                {
                    Prompt = userText,
                },
                cancellationToken: cancellationToken);

            if (response?.Data?.Content is string content && !string.IsNullOrWhiteSpace(content))
            {
                await turnContext.SendActivityAsync(content, cancellationToken: cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    "📜 *The Scribe's quill hesitates...* I couldn't conjure a response. Try again?",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            // Discard the cached session on error so it gets recreated next turn
            _sessions.TryRemove(conversationId, out _);
            Console.Error.WriteLine($"Copilot SDK error: {ex}");
            await turnContext.SendActivityAsync(
                "⚠️ *A magical disturbance disrupts the Scribe's work.* Please ensure the Copilot CLI is installed and you are logged in.\n\n" +
                "Run: `npm install -g @github/copilot && copilot auth login`",
                cancellationToken: cancellationToken
            );
        }
    }


    private static async Task<CopilotSession> GetOrCreateSessionAsync(CopilotClient client, string conversationId, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(conversationId, out CopilotSession? existing))
        {
            return existing;
        }

        string model = Environment.GetEnvironmentVariable("COPILOT_MODEL") ?? "gpt-4.1";

        CopilotSession session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Tools = [DiceRoller.CreateTool(), InventoryManager.CreateTool(conversationId)],
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = DungeonScribePersona,
            },
        }, cancellationToken);

        _sessions.TryAdd(conversationId, session);
        return session;
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
}
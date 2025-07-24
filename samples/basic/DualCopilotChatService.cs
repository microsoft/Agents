// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.CopilotStudio.Client;

namespace CopilotStudioClientSample;

/// <summary>
/// This class manages communication between two Copilot Studio agents.
/// It allows sending messages from Copilot 1 to Copilot 2 and provides an interactive chat interface.
/// </summary>
internal class DualCopilotChatService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private CopilotClient _copilot1Client;
    private CopilotClient _copilot2Client;

    public DualCopilotChatService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _copilot1Client = _serviceProvider.GetRequiredKeyedService<CopilotClient>("copilot1");
        _copilot2Client = _serviceProvider.GetRequiredKeyedService<CopilotClient>("copilot2");
    }

    /// <summary>
    /// Main service loop that provides interactive chat with both copilots
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("=== Dual Copilot Chat Service Started ===");
        Console.WriteLine("Commands:");
        Console.WriteLine("  1:<message> - Send message to Copilot 1");
        Console.WriteLine("  2:<message> - Send message to Copilot 2");
        Console.WriteLine("  relay:<message> - Send to Copilot 1, then relay response to Copilot 2");
        Console.WriteLine("  quit - Exit");
        Console.WriteLine("==========================================");

        // Initialize both copilots
        await InitializeCopilot(_copilot1Client, "Copilot 1", cancellationToken);
        await InitializeCopilot(_copilot2Client, "Copilot 2", cancellationToken);

        // Start interactive chat loop
        await InteractiveChatLoop(cancellationToken);
    }

    /// <summary>
    /// Initialize a copilot client and start its conversation
    /// </summary>
    private async Task InitializeCopilot(CopilotClient copilotClient, string name, CancellationToken cancellationToken)
    {
        Console.WriteLine($"\n[{name}] Initializing...");
        
        await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
        {
            if (act is null) continue;
            Console.WriteLine($"[{name}] {GetActivityText(act)}");
        }
        
        Console.WriteLine($"[{name}] Ready!");
    }

    /// <summary>
    /// Interactive chat loop handling user commands
    /// </summary>
    private async Task InteractiveChatLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\nCommand> ");
            string input = Console.ReadLine()!;

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.ToLower() == "quit") break;

            try
            {
                await ProcessCommand(input, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Process user commands for copilot interactions
    /// </summary>
    private async Task ProcessCommand(string input, CancellationToken cancellationToken)
    {
        if (input.StartsWith("1:"))
        {
            string message = input.Substring(2).Trim();
            await SendToCopilot(_copilot1Client, "Copilot 1", message, cancellationToken);
        }
        else if (input.StartsWith("2:"))
        {
            string message = input.Substring(2).Trim();
            await SendToCopilot(_copilot2Client, "Copilot 2", message, cancellationToken);
        }
        else if (input.StartsWith("relay:"))
        {
            string message = input.Substring(6).Trim();
            await RelayMessage(message, cancellationToken);
        }
        else
        {
            Console.WriteLine("[ERROR] Invalid command. Use 1:<message>, 2:<message>, relay:<message>, or quit");
        }
    }

    /// <summary>
    /// Send a message to a specific copilot
    /// </summary>
    private async Task SendToCopilot(CopilotClient copilotClient, string name, string message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"\n[YOU → {name}] {message}");
        Console.Write($"[{name}] ");

        await foreach (Activity act in copilotClient.AskQuestionAsync(message, null, cancellationToken))
        {
            if (act is null) continue;
            Console.Write(GetActivityText(act));
        }
        Console.WriteLine(); // New line after response
    }

    /// <summary>
    /// Relay a message from Copilot 1 to Copilot 2
    /// This demonstrates inter-copilot communication
    /// </summary>
    private async Task RelayMessage(string originalMessage, CancellationToken cancellationToken)
    {
        Console.WriteLine($"\n[RELAY] Starting relay: {originalMessage}");
        
        // Step 1: Send to Copilot 1
        Console.WriteLine($"[YOU → Copilot 1] {originalMessage}");
        Console.Write("[Copilot 1] ");
        
        string copilot1Response = "";
        await foreach (Activity act in _copilot1Client.AskQuestionAsync(originalMessage, null, cancellationToken))
        {
            if (act is null) continue;
            string actText = GetActivityText(act);
            Console.Write(actText);
            
            if (act.Type == "message" && !string.IsNullOrEmpty(act.Text))
            {
                copilot1Response += act.Text + " ";
            }
        }
        Console.WriteLine(); // New line
        
        if (string.IsNullOrWhiteSpace(copilot1Response))
        {
            Console.WriteLine("[ERROR] No response from Copilot 1 to relay");
            return;
        }

        // Step 2: Relay Copilot 1's response to Copilot 2
        string relayMessage = $"Copilot 1 said: {copilot1Response.Trim()}";
        Console.WriteLine($"\n[Copilot 1 → Copilot 2] {relayMessage}");
        Console.Write("[Copilot 2] ");
        
        await foreach (Activity act in _copilot2Client.AskQuestionAsync(relayMessage, null, cancellationToken))
        {
            if (act is null) continue;
            Console.Write(GetActivityText(act));
        }
        Console.WriteLine(); // New line
        
        Console.WriteLine("[RELAY] Complete!");
    }

    /// <summary>
    /// Extract readable text from an activity
    /// </summary>
    private static string GetActivityText(IActivity act)
    {
        return act.Type switch
        {
            "message" when act.TextFormat == "markdown" => act.Text ?? "",
            "message" => act.Text ?? "",
            "typing" => ".",
            "event" => "+",
            _ => $"[{act.Type}]"
        };
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Dual Copilot Chat Service stopped.");
        return Task.CompletedTask;
    }
}

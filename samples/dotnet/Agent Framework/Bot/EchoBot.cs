using AgentSDKWeatherAgent.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace Agent_Demo1.Bot;
public class EchoBot : AgentApplication
{
    // Create the ChatClientAgent and thread creation
    // The AF ChatClient agent wraps the IChatClient and provides tools and memory management.
    private readonly ChatClientAgent AFClient;
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new ConcurrentDictionary<string, AgentThread>();
    public EchoBot(AgentApplicationOptions options, IChatClient chatClient) : base(options)
    {
        var toolOptions = new ChatOptions
        {
            Temperature = (float?)0.2,
            Tools = new List<AITool>()
        };
        toolOptions.Tools.Add(AIFunctionFactory.Create(DateTimeFunctionTool.getDate));

        AFClient = new ChatClientAgent(chatClient, new ChatClientAgentOptions { Name = "Helper", Instructions = """You are a professional cat""", ChatOptions = toolOptions });

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
        OnActivity(ActivityTypes.Message, OnMessageAsync);
    }

    protected async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("ADD IN YOUR PROMPT FOR YOUR AGENT HERE"), cancellationToken);
            }
        }
    }
    protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var userText = turnContext.Activity.Text?.Trim() ?? string.Empty;
        var conversationId = turnContext.Activity.Conversation.Id;
        var thread = _threads.GetOrAdd(conversationId, _ => AFClient.GetNewThread());
        var result = await AFClient.RunAsync(userText, thread, cancellationToken: cancellationToken);

        await turnContext.SendActivityAsync(result.Text, cancellationToken: cancellationToken);
    }
}

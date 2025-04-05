# Microsoft 365 Agents SDK

The [Microsoft 365 Agents SDK](aka.ms/agents) allows developers to create agents that can be used in applications like Microsoft 365 Copilot, Microsoft Teams, Custom Apps, Slack, Discord and more. These agents react to events, and those events can be a conversational or operate in the background action to trigger autonomously. Developers can work with Agents built using this SDK or other AI Agents, Orchastrators and Knowledge from other providers.

## Key features of the Agents SDK

It is important for us to offer flexibility to developers to implement agents from any provider or technology stack into their enterprise. We want the Agents SDK to be the glue that makes it easy for developers to implement agentic patterns and being able to switch out services, models, agents to meet the needs and availability of the latest AI Aervices, so businesses can focus on what serves them best to solve the problems they have today, and in the future.

We want you to be able to:

1. Build an agent quickly and surface it in any channel, like Microsoft 365 Copilot or Microsoft Teams
2. We have designed this SDK to be unopinionated about the AI you use, so you can implement agentic patterns without being locked to a tech stack
3. It is important that this SDK is works with specific client behaviour, like Microsoft Teams or SharePoint, to allow you to tailor your agent to clients, such as specific events or actions.

## Create an agent

It is easy to get the starter sample in C#, JavaScript or Python from [Github](https://github.com/microsoft/Agents/tree/main/samples/basic/echo-bot)

To create an agent in C#: 

```
builder.AddAgent( sp =>
{
    var agent = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var text = turnContext.Activity.Text;
        await turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {text}"), cancellationToken);
    });
});
```
        
</pre>

This creates a new agent, listens for a message type activity and sends a message back

Now your ready to add your chosen custom AI Services (e.g Azure Foundry or OpenAI Agents) & Orchestration (e.g. Semantic Kernel) into your agent.

## Important terms

Some specific concepts that are important to the SDK are:

- `Turns` - A turn is a unit of work that is done by the agent. It can be a single message or a series of messages. Developers will work with 'turns' and manage the data between them
- `Activity` - An activity is one or more type of unit that is managed by the agent
- `Messages` - A message is a type of activity that is sent to the agent. It can be a single message or a series of messages.

## Fundamentals

- [Quickstart C#](./docs/quickstart-csharp.md) (JavaScript & Python Quickstarts coming soon) 
- [Building an Agent](./docs/buildingagents.md)
- [Managing Turns](./docs/managingturns.md)
- [Using Activities](./docs/usingactivities.md)
- [Creating Messages](./docs/creatingmessages.md)

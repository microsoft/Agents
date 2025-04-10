---
layout: custom
title: Building Agents
---

# Building Agents

In the Microsoft 365 Agents SDK, an agent container is built using `Agent Application` and `Agent Applicatiion Options`

`Agent Application Options` allows for different configuration options including authentication. It is an parameter in the constructor of `Agent Application`


```
builder.AddAgentApplicationOptions();

builder.AddAgent(sp =>
{
    // Setup the Agent. 
    var agent = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());

    // Respond to message events. 
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var text = turnContext.Activity.Text;
        await turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {text}"), cancellationToken);
    });
    return agent;
});
```

The endpoint of your application needs to be configured to handle incoming requests and is designed to specifically process messages to your agent, using an adapter `IBotHttpadapter` and `IBot agent` (which is the agent created earlier) this converts the http request and response into understandable formats between the web server and the agent.

```cs
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IBotHttpAdapter adapter, IBot agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});
```

Now the agent is created, you can register to listen for events, add your AI services and custom logic.

In the simple samples on the [repo](aka.ms/agents), you will see the agent is registered automatically for the generic `OnActivity` event. 
```cs
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var text = turnContext.Activity.Text;
        await turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {text}"), cancellationToken);
    });
```
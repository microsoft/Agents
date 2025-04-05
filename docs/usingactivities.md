---
layout: custom
title: Using activities
---

# Using Activities

`Activities` are the main object that is recieved by your agent, and sent back. This interaction is commonly refered to as the `Activity Protocol`. 

Your agent listens for events that are types of `Activities` created between the client and your agent. The Microsoft 365 Agents SDK has `Channel Adapters` that translate the different channel languages into `Activities`

Activities are used in 2 places:

1. When listening for events. The agent listens for `Activities`. In the example below, the agent is listening for an `Activity` of type `Message` that can contain any type of text, media, etc. 

```
    agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        // custom logic here
    });
```

2. When sending responses, developers can construct a `Message` using `MessageFactory` and send it in the `SendActivityAsync` method. 

```
    await turnContext.SendActivityAsync(MessageFactory.Text({response}"), cancellationToken);
```


## Activity Types
### Message
The most common type of activity, used to exchange text, media, and rich content between the bot and the user, e.g. A user sends a text message to the agent, and the agent replies with a text message.

```
agent.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken))
```

### Conversation Update
Used to notify the agent when members are added to or removed from a conversation, e.g. A user joins a group chat, and the agent is notified of the new participant.

```
agent.OnActivity(ActivityTypes.ConversationUpdate, async (turnContext, turnState, cancellationToken) =>
{
    var membersAdded = turnContext.Activity.MembersAdded;
    if (membersAdded != null && membersAdded.Any())
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the chat, {member.Name}!"), cancellationToken);
            }
        }
    }
});
```

### Typing
Indicates that the sender is typing a message, e.g. The agent sends a typing indicator to show that it is processing the user's message.

```
agent.OnActivity(ActivityTypes.Typing, async (turnContext, turnState, cancellationToken) =>
{
    Console.WriteLine("User is typing...");
    await Task.CompletedTask;
});
```

### End of Conversation
Indicates that the conversation has ended, e.g. The agent sends an end-of-conversation activity when it has completed its interaction with the user. Useful if you wanted to specifically send pre-built feedback forms or just simply, show a conversation has ended.

```
    agent.OnActivity(ActivityTypes.EndOfConversation, async (turnContext, turnState, cancellationToken) =>
    {
        await turnContext.SendActivityAsync(MessageFactory.Text("Goodbye!"), cancellationToken);
    });
```

### Custom Event
Used to send and receive events, which are lightweight messages that can carry custom data. e.g. The agent sends an event to trigger a specific action in the client application.

```
    agent.OnActivity(ActivityTypes.Event, async (turnContext, turnState, cancellationToken) =>
    {
        var eventActivity = turnContext.Activity.AsEventActivity();
        if (eventActivity.Name == "customEvent")
        {
            var value = eventActivity.Value?.ToString();
            await turnContext.SendActivityAsync(MessageFactory.Text($"Received custom event with value: {value}"), cancellationToken);
        }
    });
```

There are other types of `Activities` that are specific to clients, such as Microsoft Teams (e.g. `Message Reaction`). Custom Apps or Websites will also have their own events and to interact with these events, developers can use the `Custom Event` activity type.
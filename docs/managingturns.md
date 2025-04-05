---
layout: custom
title: Managing Turns
---

# Managing Turns

The Microsoft 365 Agents SDK is based on turns. A turn is the roundtrip from the incoming `activity` and the any outgoing `activity` that you choose to send back. 

A turn can contain many `Activities` where multiple messages are sent back to a person or other agent. A turn does not mean the conversation or interaction has ended, only that specific turn. Typically within a conversation multiple 'turns' take place. A turn therefore represents the workflow of a outgoing action of the agent.

Multiple internal actions can take place within a turn, such as using internal services, AI services, APIs and more to generate plans, responses and get information to use & format. 

There is information available to the agent per turn based on the `turn context`

```
    await turnContext.SendActivityAsync(MessageFactory.Text({response}"), cancellationToken);
```

The `turn context` is how activities are sent using the `SendActivityAync` method

Agents created with the Agents SDK are stateless by default and can be added. 

State is supported as an optional parameter when the agent is built, and includes `Private State`, `User State` and `Conversation State`. State requires storage, of which the SDK supports `Memory` storage, `Blob`, `Cosmos` or your own custom storage that can be used to store state (and other information you require).

Because by default, they are stateless, any information is lost on the next turn, unless it is saved.

### Using state across multiple agents

When working with multiple agents developers may want to manage multiple states of agents. This will be dependent on the types of state and storage supported by those agents and what would be required to be stored persistantly, and what data you want or need to store across turns.
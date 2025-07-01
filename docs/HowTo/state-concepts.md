# Managing state

APPLIES TO: Agents SDK

State within a agent follows the same paradigms as modern web applications, and the Agent SDK provides some abstractions to make state management easier.

As with web apps, a agent is inherently stateless; a different instance of your agent may handle any given turn of the conversation. For some agents, this simplicity is preferred—the agent can either operate without additional information, or the information required is guaranteed to be within the incoming message. For others, state (such as where the conversation left off or data previously received about the user) is necessary for the agent to have a useful conversation.

## Why do I need state?

Maintaining state allows your agent to have more meaningful conversations by remembering certain things about a user or conversation. For example, if you've talked to a user previously, you can save previous information about them, so that you don't have to ask for it again. State also keeps data for longer than the current turn, so that your agent keeps information over the course of a multi-turn conversation.

As it pertains to agents, there are a few layers to using state: the storage layer, state management, and AgentApplication.

## Storage layer
Starting at the backend, where the state information is actually stored, is the storage layer. This can be thought of as your physical storage, such as in-memory, Azure, or a third party server.

The Agents SDK includes some implementations for the storage layer:

Memory storage implements in-memory storage for testing purposes. In-memory data storage is intended for local testing only as this storage is volatile and temporary. The data is cleared each time the agent is restarted.
Azure Blob Storage connects to an Azure Blob Storage object database.
Azure Cosmos DB partitioned storage connects to a partitioned Cosmos DB NoSQL database.
 
For instructions on how to connect to other storage options, see [Agents SDK Storage Overview](storage.md)

## State management
State management automates the reading and writing of your agent's state to the underlying storage layer. State is stored as state properties, which are effectively key-value pairs that your agent can read and write through the state management object without worrying about the specific underlying implementation. Those state properties define how that information is stored. For example, when you retrieve a property that you defined as a specific class or object, you know how that data will be structured.

These state properties are lumped into scoped "buckets", which are just collections to help organize those properties. The SDK includes three of these "buckets":

- User state
- Conversation state

All of these buckets are subclasses of the agent state class, which can be derived to define other types of buckets with different scopes.

These predefined buckets are scoped to a certain visibility, depending on the bucket:

- User state is available in any turn that the agent is conversing with that user on that channel, regardless of the conversation
- Conversation state is available in any turn in a specific conversation, regardless of user, such as in group conversations

> agenth user and conversation state are scoped by channel. The same person using different channels to access your agent appears as different users, one for each channel, and each with a distinct user state.

The keys used for each of these predefined buckets are specific to the user and conversation, or agenth. When setting the value of your state property, the key is defined for you internally, with information contained on the turn context to ensure that each user or conversation gets placed in the correct bucket and property. Specifically, the keys are defined as follows:

- The user state creates a key using the channel ID and from ID. For example, `{Activity.ChannelId}/users/{Activity.From.Id}#YourPropertyName`
- The conversation state creates a key using the channel ID and the conversation ID. For example, `{Activity.ChannelId}/conversations/{Activity.Conversation.Id}#YourPropertyName`

### When to use each type of state
Conversation state is good for tracking the context of the conversation, such as:
- Whether the agent asked the user a question, and which question that was
- What the current topic of conversation is, or what the last one was
- Recording chat history

User state is good for tracking information about the user, such as:
- Non-critical user information, such as name and preferences, an alarm setting, or an alert preference
- Information about the last conversation they had with the agent
  - For instance, a product-support agent might track which products the user has asked about.

## AgentApplication
- Route handlers you add will be provided with a `TurnState` instance.  Access conversation or user state from this instance.
- State is automaticlaly loaded and saved.

## Further reading
- [Using storage providers in your agent](./storage.md)

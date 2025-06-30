# Microsoft 365 Agents SDK

The Microsoft 365 Agents SDK allows developers to create agents that can be used in applications like Microsoft 365 Copilot, Microsoft Teams, Custom Apps, Slack, Discord and more. These agents react to events, and those events can be a conversational or operate in the background action to trigger autonomously. Developers can work with Agents built using this SDK or other AI Agents, Orchastrators and Knowledge from other providers.

## Key features of the Agents SDK

It is important for us to offer flexibility to developers to implement agents from any provider or technology stack into their enterprise. We want the Agents SDK to be the glue that makes it easy for developers to implement agentic patterns and being able to switch out services, models, agents to meet the needs and availability of the latest AI Aervices, so businesses can focus on what serves them best to solve the problems they have today, and in the future.

We want you to be able to:

1. Build an agent quickly and surface it in any channel, like Microsoft 365 Copilot or Microsoft Teams
2. We have designed this SDK to be unopinionated about the AI you use, so you can implement agentic patterns without being locked to a tech stack
3. It is important that this SDK is works with specific client behaviour, like Microsoft Teams or SharePoint, to allow you to tailor your agent to clients, such as specific events or actions.

## Topics

- QuickStart with the EmptyAgent sample
  - [DotNet](../samples/basic/empty-agent/dotnet/README.md)
  - [JS](../samples/basic/empty-agent/nodejs/README.md)
- [Samples](../samples/README.md)
- Provisioning a new Azure Bot
  - [Federated Credentials](HowTo/azurebot-create-fic.md)
  - [Managed Identity](HowTo/azurebot-create-msi.md)
  - [SingleTenant with ClientSecret](HowTo/azurebot-create-single-secret.md)
  - Configuring the Agents Auth settings
    - [DotNet](HowTo/MSALAuthConfigurationOptions.md)
    - [JS](HowTo/azurebot-auth-for-js.md)
- Adding OAuth
  - [Federated Credentials OAuth](HowTo/azurebot-user-authentication-fic.md)
- State and Storage
  - [Changing Storage providers](HowTo/storage.md)

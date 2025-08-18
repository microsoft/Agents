# azure-ai-streaming agent

This is a sample of a simple Agent that is hosted on an Node.js web service with the Express framework. This agent is configured to accept a request and forward to an LLM that will return a poem using Azure OpenAI

This agent sample is intended to introduce you the basics of implementing Stream Responses with the Microsoft 365 Agents SDK in order to build powerful agents. It can also be used as a the base for a custom agent that you choose to develop.

## Prerequisites

- NodeJS > 20.*
- You will need an Azure OpenAI or OpenAI instance, with the preferred model of `gpt-4o-mini`.

## Running this sample

Create a `.env` file, based on the provided `env.TEMPLATE` and configure either the AzureOpenAI or OpenAI settings:

```env
AZURE_RESOURCE_NAME=
AZURE_API_KEY=
AZURE_OPENAI_DEPLOYMENT_NAME=
```

## Getting started with the azure-ai-streaming sample

### QuickStart using the Microsoft 365 Agents Playground

1. Open the `nodejs/azure-ai-streaming` sample in Visual Studio Code
1. Start the application with  `npm start`
1. Start the test tool with `npm run test-tool`

If all is working correctly, the Agents Playground tool should show you a web chat experience with the words **Welcome to the Streaming sample, type poem to see the streaming feature in action.**, now you can interact with the agent typing the word **poem** 

### QuickStart using WebChat

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your bot
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.


1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below


1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.

   1. Update the `.env` file in the root of the sample project.

```env
tenantId=
clientId=
clientSecret=
```
   
1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent with `npm start`

1. Go to Azure Bot Service `Test in WebChat` to start interacting with your bot.

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

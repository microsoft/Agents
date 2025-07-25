# WeatherAgent Sample with LangChain

This is a sample of a simple Weather Forecast Agent that is hosted on a express core web service.  This Agent is configured to accept a request asking for information about a weather forecast and respond to the caller with an Adaptive Card.

This Agent Sample is intended to introduce you the basics of integrating LangChain with the Microsoft 365 Agents SDK in order to build powerful Agents. It can also be used as a the base for a custom Agent that you choose to develop.

***Note:*** This sample requires JSON output from the model which works best from newer versions of the model such as gpt-4o-mini.

## Prerequisites

- NodeJS > 20.*
- 1. You will need an Azure OpenAI or OpenAI instance, with the preferred model of `gpt-4o-mini`.

## Running this sample

Create a `.env` file, based on the provided `env.TEMPLATE` and configure either the AzureOpenAI or OpenAI settings:

```env
AZURE_OPENAI_API_INSTANCE_NAME=
AZURE_OPENAI_API_DEPLOYMENT_NAME=
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_API_VERSION=

OPENAI_MODEL=
OPENAI_API_KEY=

USE_AZURE_OPENAI_API=true
```

## Getting Started with WeatherBot Sample

Read more about [Running an Agent](../../../docs/HowTo/running-an-agent.md)

### QuickStart using Teams app test tool

1. Open the weatherbot Sample in Visual Studio Code
1. Start the application with  `npm start`
1. Start the test tool with `npm run test-tool`

If all is working correctly, the Teams app test tool should show you a Web Chat experience with the words **"Hello and Welcome! I'm here to help with all your weather forecast needs!"**, now you can interact with the agent asking forecast questions such as **tell me the weather forecast for today in NYC** 

> [!Important]
> The Teams app test tool does not support authentication, to use it you should not configure the `clientId`, this is only required for Azure Bot Service

### QuickStart using WebChat

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your bot
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your bot on to.
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
   
1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent with `npm start`

1. Go to Azure Bot Service `Test in WebChat` to start interacting with your bot.

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

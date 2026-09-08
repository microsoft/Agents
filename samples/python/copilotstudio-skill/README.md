# CopilotStudio Skill

This sample shows how to create an Agent that can be consumed from CopilotStudio as a skill.

## Prerequisites

- [Python](https://www.python.org/) version 3.10 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)
- Access to CopilotStudio to [create an Agent](https://learn.microsoft.com/microsoft-copilot-studio/fundamentals-get-started?tabs=web)

## Deploy in localhost

The first time you setup the agent to be consumed as a skill, you might want to do it in `localhost`, so you can debug and revisit the desired configuration before deploying to azure. 
To do so you can use the tool [Dev Tunnels](https://aka.ms/devtunnels) to expose an endpoint in your machine to the internet. 

```bash
devtunnel host -a -p 3978
```

Take note of the tunnelUrl

1. Create an Azure Bot Service resource, and save the authentication information in the `.env` file:

    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
      2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
      3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.

1. Update the Home page URL in the app registration with the tunnel url.
1. Update the manifest replacing the `{baseUrl}` with the `{tunnelUrl}` and `{clientId}` with the `{appId}` from EntraID
1. - [Configure a skill](https://learn.microsoft.com/microsoft-copilot-studio/configuration-add-skills#configure-a-skill)
    1. In CopilotStudio navigate to the Agent settings, skills, and register the skill with the URL `{tunnelUrl}/manifest.json`
    1. In CopilotStudio navigate to the Agent topics, add a new trigger to invoke an action to call the skill

## Deploy in Azure

1. Deploy the express application to Azure, using AppService, AzureVMs or Azure Container Apps.
1. Update the Home page URL in the app registration with the Azure service URL.
1. Create an Azure Bot Service resource, and save the authentication information in the `.env` file:
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
      2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
      3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.
1. - [Configure a skill](https://learn.microsoft.com/microsoft-copilot-studio/configuration-add-skills#configure-a-skill)
    1. In CopilotStudio navigate to the Agent settings, skills, and register the skill with the URL `{azureServiceUrl}/manifest.json`
    1. In CopilotStudio navigate to the Agent topics, add a new trigger to invoke an action to call the skill


## Running this sample

1. Once the app is running, in localhost or in Azure
1. Use the CopilotStudio _Test your agent_ chat UI, and use the trigger defined to invoke the skill

## Further Reading
For more information on logging configuration, see the logging section in the Quickstart Agent sample README.

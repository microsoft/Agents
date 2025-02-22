# copilotstudio-skill

This sample shows how to create an Agent that can be consumed from CopilotStudio as a skill.

## Prerequisites

- [Node.js](https://nodejs.org) version 20 or higher

    ```bash
    # determine node version
    node --version
    ```

- Access to CopilotStudio to [create an Agent](https://learn.microsoft.com/en-us/microsoft-copilot-studio/fundamentals-get-started?tabs=web)

## Deploy in localhost

The first time you setup the agent to be consumed as a skill, you might want to do it in `localhost`, so you can debug and revisit the desired configuration before deploying to azure. 
To do so you can use the tool [Dev Tunnels](https://aka.ms/devtunnels) to expose an endpoint in your machine to the internet. 

```bash
devtunnel host -a -p 3978
```

Take note of the tunnelUrl

1. Create an EntraID app registration, and save the authentication information in the `.env` file, following any of the [available options](https://microsoft.github.io/Agents/HowTo/azurebot-auth-for-js.html) for Single Tenant.
1. Update the Home page URL in the app registration with the tunnel url.
1. Update the manifest replacing the `{baseUrl}` with the `{tunnelUrl}` and `{clientId}` with the `{appId}` from EntraID
1. - [Configure a skill](https://learn.microsoft.com/en-us/microsoft-copilot-studio/configuration-add-skills#configure-a-skill)
    1. In CopilotStudio navigate to the Agent settings, skills, and register the skill with the URL `{tunnelUrl}/manifest.json`
    1. In CopilotStudio navigate to the Agent topics, add a new trigger to invoke an action to call the skill

## Deploy in Azure

1. Deploy the express application to Azure, using AppService, AzureVMs or Azure Container Apps.
1. Update the Home page URL in the app registration with the Azure service URL.
1. Create an EntraID app registration, and configure the Azure instance following any of the [available options](https://microsoft.github.io/Agents/HowTo/azurebot-auth-for-js.html) for SingleTenant.
1. - [Configure a skill](https://learn.microsoft.com/en-us/microsoft-copilot-studio/configuration-add-skills#configure-a-skill)
    1. In CopilotStudio navigate to the Agent settings, skills, and register the skill with the URL `{azureServiceUrl}/manifest.json`
    1. In CopilotStudio navigate to the Agent topics, add a new trigger to invoke an action to call the skill


## Running this sample

1. Once the express app is running, in localhost or in Azure
1. Use the CopilotStudio _Test your agent_ chat UI, and use the trigger defined to invoke the skill

# Helper Scripts

Replace Manifest Values

```ps
$url='{tunnelUrl}'
$clientId='{clientId}'
(Get-Content public/manifest.template.json).Replace('{baseUrl}', $url).Replace('{clientId}', $clientId) | Set-Content public/manifest.json
```

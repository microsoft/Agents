# Slack Sample

> This is a preview version of the Slack AgentExtension in Agents SDK.

This is a sample of a simple slack Agent that is hosted on an Asp.net core web service and intended to introduce you the basic operation of the Microsoft 365 Agents Slack AgentExtensions.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)
- An Azure subscription. If you don't already have one, create a free account before you begin.
- Access to a Slack workspace with sufficient permissions to create and manage applications at https://api.slack.com/apps. If you don't have access to a Slack environment, you can create a workspace.
 
## QuickStart using Slack

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)
   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials) 
   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)
    
   > Be sure to follow the **Next Steps** at the end of these docs to configure your agent settings.

   > **IMPORTANT:** If you want to run your agent locally via devtunnels, the only supported auth type is ClientSecrets and Certificates

1. Running the Agent
   1. Running the Agent locally
      - Requires a tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.
      - **For ClientSecret or Certificate authentication types only.**  Federated Credentials and Managed Identity will not work via a tunnel to a local agent and must be deployed to an App Service or container.
      
      1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

         ```bash
         devtunnel host -p 3978 --allow-anonymous
         ```

      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

      1. Start the Agent in Visual Studio

1. Create and configure a Slack App to connect to your Agent
   1. Follow the instructions in this doc to create a Slack App and connect it to your Agent: https://learn.microsoft.com/en-us/azure/bot-service/bot-service-channel-connect-slack?view=azure-bot-service-4.0
 
## Enabling JWT token validation
1. By default, token validation is disabled in Development mode.  This is determined by `AddAgentAuthorization` and the `forceEnable` argument.

1. Updating appsettings and replace {{ClientId}} and {{TenantId}} with the values from your Azure Bot.
   ```json
   "TokenValidation": {
     "Audiences": [
       "{{ClientId}}"
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
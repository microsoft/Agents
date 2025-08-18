# AutoSignIn

This Agent has been created using [Microsoft 365 Agents Framework](https://github.com/microsoft/agents-for-net), it shows how to use Auto SignIn user authorization in your Agent.

This sample:
- Gets an OAuth token automatically for every message sent by the user
  > This is done by setting the `AgentApplication:UserAuthorization:AutoSignIn` setting to true.  This will use the default UserAuthorization Handler to automatically get a token for all incoming Activities.  Use this when your Agents needs the same token for much of it's functionality.
- Per-Route sign in .  In this sample, this is the `-me` message.
  > Messages the user sends are routed to a handler of your choice.  This feature allows you to indicate that a particular token is needed for the handler.  Per-Route user authorization will automatically handle the OAuth flow to get the token and make it available to your Agent.  Keep in mind that if the the Auto SignIn option is enabled, you actually have two tokens available in the handler.

The sample uses the bot OAuth capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop a bot that authorizes users to various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)

## QuickStart using WebChat or Teams

- Overview of running and testing an Agent
  - Provision an Azure Bot in your Azure Subscription
  - Configure your Agent settings to use to desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)
  - Test in a client

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)
   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials) 
   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)
    
   > Be sure to follow the **Next Steps** at the end of these docs to configure your agent settings.

   > **IMPORTANT:** If you want to run your agent locally via devtunnels, the only support auth type is ClientSecrets and Certificates

1. [Add OAuth to your bot](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-user-authorization-federated-credentials)
   1. For ease of setup, you can use just one OAuth Connection for both handlers (below).

1. Configure the UserAuthorization handlers
   1. Open the `appsettings.json` file and locate
      ```json
      "AgentApplication": {
        "UserAuthorization": {
          "DefaultHandlerName": "auto",
          "AutoSignIn": true,
          "Handlers": {
            "auto": {
              "Settings": {
                "AzureBotOAuthConnectionName": "{{auto_connection_name}}",
                "Title": "SigIn for Sample",
                "Text":  "Please sign in and send the 6-digit code"
              }
            },
           "me": {
             "Settings": {
               "AzureBotOAuthConnectionName": "{{me_connection_name}}",
               "Title": "SigIn for Me",
               "Text": "Please sign in and send the 6-digit code"
             }
          }
        }
      }
      ```

      1. Replace **{{auto_connection_name}}** with the first **OAuth Connection** name created
      1. Replace **{{me_connection_name}}** with the second **OAuth Connection** name created
      
      > For more information on OAuth see [Configure your .NET Agent to use OAuth](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/agent-oauth-configuration-dotnet)

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

   1. Deploy Agent code to Azure
      1. VS Publish works well for this.  But any tools used to deploy a web application will also work.
      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `https://{{appServiceDomain}}/api/messages`

## Testing this agent with WebChat

   1. Select **Test in WebChat** on the Azure Bot

## Testing this Agent in Teams or M365

1. Update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
     - `manifest.json`
     - `outline.png`
     - `color.png`

1. Your Azure Bot should have the **Microsoft Teams** channel added under **Channels**.

1. Navigate to the Microsoft Admin Portal (MAC). Under **Settings** and **Integrated Apps,** select **Upload Custom App**.

1. Select the `manifest.zip` created in the previous step. 

1. After a short period of time, the agent shows up in Microsoft Teams and Microsoft 365 Copilot.

## Interacting with the Agent

- When the conversation starts, you will be greeted with a welcome message which include your name and instructions.  If this is the first time you've interacted with the Agent in a conversation, you will be prompts to sign in (WebChat), or with Teams SSO the OAuth will happen silently.
- Sending `-me` will display additional information about you.
- Note that if running this in Teams and SSO is setup, you shouldn't see any "sign in" prompts.  This is true in this sample since we are only requesting a basic set of scopes that Teams doesn't require additional consent for.

## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Enabled": true,
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further reading
- [Configure your .NET Agent to use OAuth](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/agent-oauth-configuration-dotnet)
- To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).


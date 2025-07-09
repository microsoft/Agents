# AutoSignIn

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-net), it shows how to use Auto SignIn user authorization in your Agent.

This sample:
- Gets an OAuth token automatically for every message sent by the user
  > This is done by setting the `AgentApplication:UserAuthorization:AutoSign` setting to true.  This will use the default UserAuthorization Handler to automatically get a token for all incoming Activities.  Use this when your Agents needs the same token for much of it's functionality.
- Per-Route sign in .  In this sample, this is the `-me` message.
  > Messages the user sends are routed to a handler of your choice.  This feature allows you to indicate that a particular token is needed for the handler.  Per-Route user authorization will automatically handle the OAuth flow to get the token and make it available to your Agent.  Keep in mind that if the the Auto SignIn option is enabled, you actually have two tokens available in the handler.

The sample uses the bot OAuth capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop a bot that authorizes users to various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)

## Running this sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. [Add OAuth to your bot](https://aka.ms/AgentsSDK-AddAuth)

1. For purposes of this sample, create a second Azure Bot **OAuth Connection** as a copy of the one just created.
   > This is to ease setup of this sample.  In an actual Agent, this second connection would be setup separately, with different API Permissions and scopes specific to the external service being accessed. \
   > This OAuth Connection is for the `-me` message in this sample.   

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
          "ServiceConnection": {
          "Settings": {
              "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
              "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
              "ClientId": "{{ClientId}}", // this is the Client ID used for the connection.
              "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
              "Scopes": [
                "https://api.botframework.com/.default"
              ]
          }
      }
      ```

      1. Replace all **{{ClientId}}** with the AppId of the bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Configure the UserAuthorization handlers
   1. Open the `appsettings.json` file and locate
      ```json
      "AgentApplication": {
        "UserAuthorization": {
          "DefaultHandlerName": "auto",
          "AutoSignin": true,
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

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Update your Azure Bot ``Messaging endpoint`` with the tunnel Url:  `{tunnel-url}/api/messages`

1. Run the bot from a terminal or from Visual Studio

1. Test via "Test in WebChat"" on your Azure Bot in the Azure Portal.

## Running this Agent in Teams

1. There are two version of the manifest provided.  One for M365 Copilot and one for Teams.
   1. Copy the desired version to manifest.json
1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<AGENT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Select **Preview in Teams** in the upper right corner

## Interacting with the Agent

- When the conversation starts, you will be greeted with a welcome message which include your name and instructions.  If this is the first time you've interacted with the Agent in a conversation, you will be prompts to sign in (WebChat), or with Teams SSO the OAuth will happen silently.
- Sending `-me` will display additional information about you.
- Note that if running this in Teams and SSO is setup, you shouldn't see any "sign in" prompts.  This is true in this sample since we are only requesting a basic set of scopes that Teams doesn't require additional consent for.

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.


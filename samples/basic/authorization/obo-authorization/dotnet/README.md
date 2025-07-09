# OBO OAuth

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-net), it shows how to use authorization in your Agent using OAuth and OBO.

- The sample uses the Agent SDK User Authorization capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop an Agent that authorizes users with various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.
- This sample shows how to use an OBO Exchange to communicate with Microsoft Copilot Studio using the CopilotStudioClient.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
   1. Publish your newly created Copilot
   1. Go to Settings => Advanced => Metadata and copy the following values. You will need them later:
      1. Schema name
      1. Environment Id
       
2. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use in the "ServiceConnection" settings below.

3. Setting up OAuth for an exchangeable token 
   1. Create a new App Registration
      1. SingleTenant
      1. Give it a name and click **Register**
      1. **Authentication** tab
         1. **Add Platform**, then **Web**, Set `Redirect URI` to `Web` and `https://token.botframework.com/.auth/web/redirect`
         1. **Add Platform**, then **Mobile and desktop applications**, and add an additional `http://localhost` Uri.
      1. **API Permissions** tab
         1. **Dynamics CRM** with **user_impersonation**
         1. **Graph** with **User.Read**
         1. **Power Platform API** with **CopilotStudio.Copilots.Invoke**
         1. Grant Admin Consent for your tenant.
      1. **Expose an API** tab
         1. Click **Add a Scope**
         1. **Application ID URI** should be: `api://botid-{{appid}}`
         1. **Scope Name** is "defaultScope"
         1. **Who can consent** is **Admins and users**
         1. Enter values for the required Consent fields
      1. **Certificates & secrets**
         1. Create a new secret and record the value.  This will be used later.
         
4. Create Azure Bot **OAuth Connection**
   1. On the Azure Bot created in Step #2, Click **Configuration** tab then the **Add OAuth Connection Settings** button.
   1. Enter a **Name**.  This will be used later.
   1. For **Service Provider** select **Azure Active Directory v2**
   1. **Client id** and **Client Secret** are the values created in step #3.
   1. Enter the **Tenant ID**
   1. **Scopes** is `api://botid-{{appid}}/defaultScope`

1. Configuring the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL authorization provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
          "ServiceConnection": {
          "Settings": {
              "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.authorization.Msal.Model.AuthTypes.  The default is ClientSecret.
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
      
   1. In the same section, edit the **MCSConnection**
      ```json
        "MCSConnection": {
        "Settings": {
          "AuthType": "ClientSecret",
          "AuthorityEndpoint": "https://login.microsoftonline.com/{{OAuthTenantId}}",
          "ClientId": "{{OAuthClientId}}", // this is the Client ID created in Step #3
          "ClientSecret": "{{OAuthClientSecret}}" // this is the Client Secret created in Step #3
        }
      ```
      1. Set **{{OAuthTenantId}}** to the AppId created in Step #3
      1. Set **{{OAuthClientSecret}}** to the secret created in Step #3
      1. Set **{{OAuthTenantId}}**
      
   1. In appsettings, replace **{{AzureBotOAuthConnectionName}}** with the OAuth Connection Name created in Step #4.

   1. Setup the Copilot Studio Agent information
      ```json
      "CopilotStudioAgent": {
        "EnvironmentId": "", // Environment ID of environment with the CopilotStudio App.
        "SchemaName": "", // Schema Name of the Copilot to use
      }
      ```
   
1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Update your Azure Bot ``Messaging endpoint`` with the tunnel Url:  `{tunnel-url}/api/messages`

1. Run the bot from a terminal or from Visual Studio

1. Test via "Test in WebChat"" on your Azure Bot in the Azure Portal.

## Running this Agent in Teams

1. There are two version of the manifest provided.  One for M365 Copilot and one for Teams.
   1. Copy the desired version to `manifest.json`.  This will typically be `teams-manifest.json` for Teams.
1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
     - `manifest.json`
     - `outline.png`
     - `color.png`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Select **Preview in Teams** in the upper right corner

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.


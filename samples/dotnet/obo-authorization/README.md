# OBO OAuth

This Agent has been created using [Microsoft 365 Agents Framework](https://github.com/microsoft/agents-for-net), it shows how to use authorization in your Agent using OAuth and OBO.

- The sample uses the Agent SDK User Authorization capabilities in Azure Bot Service, providing features to make it easier to develop an Agent that authorizes users with various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.
- This sample shows how to use an OBO exchange to communicate with Microsoft Copilot Studio using the `CopilotStudioClient`.  Any message sent from the client, for example Teams or WebChat, are forwarded to a Copilot Studion agent.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
   1. Publish your newly created Copilot
   1. Go to **Settings** => **Advanced** => **Metadata** and note the following values:
      1. Schema name
      1. Environment Id
   1. Set the Copilot Studio Agent information in appsettings
      ```json
        "CopilotStudioAgent": {
          "EnvironmentId": "", // Environment ID of environment with the CopilotStudio App.
          "SchemaName": "", // Schema Name of the Copilot to use
        }
      ```
    
2. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)
   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials) 
   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)
    
   > Be sure to follow the **Next Steps** at the end of these docs to configure your agent settings.

   > **IMPORTANT:** If you want to run your agent locally via devtunnels, the only support auth type is ClientSecrets and Certificates

3. Setting up OAuth

   > **IMPORTANT:** For Teams SSO, OAuth is always setup on the same App Registration used for the agent.  To make setting this sample up easier for other channels, do not create another App Registration; Do it all on the agents App Registration.

   1. Perform the steps in [Add user authorization using Federated Identity Credential](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-user-authorization-federated-credentials), with the following difference:
      - **API Permissions** tab
        1. **Dynamics CRM** with **user_impersonation**
        1. **Graph** with **User.Read**
        1. **Power Platform API** with **CopilotStudio.Copilots.Invoke**
        1. Grant Admin Consent for your tenant.
         
4. [Configure your .NET Agent to use OAuth](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/agent-oauth-configuration-dotnet)
   
5. Running the Agent
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
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).


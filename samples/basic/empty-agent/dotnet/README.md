# EmptyAgent Sample

This is a sample of a simple Agent that is hosted on an Asp.net core web service.  This Agent is configured to accept a request and echo the text of the request back to the caller.

This Agent Sample is intended to introduce you the basic operation of the Microsoft 365 Agents SDK messaging loop. It can also be used as a the base for a custom Agent that you choose to develop.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)

## QuickestStart using Agent Toolkit
1. If you haven't done so already, install the Agents Playground
 
   ```
   winget install agentsplayground
   ```
1. Start the Agent in VS or VS Code
1. Start the Teams App Tester.  At a command prompt: `teamsapptester`
   - The tool will open a web browser showing the Teams App Test Tool, ready to send messages to your agent. 
   - To use WebChat in the Test Tool, start with: `teamsapptester --channel-id webchat`
1. Interact with the Agent via the browser

## QuickStart using WebChat or Teams

- Overview of running and testing an Agent
  - Provision an Azure Bot in your Azure Subscription
  - Configure your Agent settings to use to desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)
  - Test in a client

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](../../../../docs/HowTo/azurebot-create-single-secret.md)
   - [SingleTenant, Federated Credentials](../../../../docs/HowTo/azurebot-create-fic.md) 
   - [User Assigned Managed Identity](../../../../docs/HowTo/azurebot-create-msi.md)

1. Configuring the authentication connection in the Agent settings
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{BOT_TENANT_ID}}",
            "ClientId": "{{BOT_ID}}", // this is the Client ID used for the connection.
            "ClientSecret": "{{BOT_SECRET}}", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      ```

      1. Replace all **{{BOT_ID}}** with the AppId of the Azure Bot.
      1. Replace all **{{BOT_TENANT_ID}}** with the Tenant Id where your application is registered.
      1. Set the **{{BOT_SECRET}}** to the Secret that was created on the App Registration.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

   1. These instructions are for **SingleTenant, Client Secret**.  For different configurations see:
      - [Federated Credentials](https://microsoft.github.io/Agents/HowTo/MSALAuthConfigurationOptions.html#federatedcredentials) 
      - [User Assigned Managed Identity](https://microsoft.github.io/Agents/HowTo/MSALAuthConfigurationOptions.html#usermanagedidentity)
      - [Client Secret](https://microsoft.github.io/Agents/HowTo/MSALAuthConfigurationOptions.html#singletenant-with-clientsecret)

1. Running the Agent
   1. Running the Agent locally
      - Requires a tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.
      - **For ClientSecret or Certificate authentication types only.**  Federated Credentials and Managed Identity will not work via a tunnel to a local agent and must be deployed to an App Service or container.
      
      1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

         ```bash
         devtunnel host -p 3978 --allow-anonymous
         ```

      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

      1. Start the Agent in Visual Studio

   1. Deploy Agent code to Azure
      1. VS Publish works well for this.  But any tools used to deploy a web application will also work.
      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `https://{{appServiceDomain}}/api/messages`

1. Testing this agent with WebChat
   1. Select **Test in WebChat** on the Azure Bot

1. Running this Agent in Teams

   1. There are two version of the manifest provided.  One for M365 Copilot and one for Teams.
      1. Copy the desired version to `manifest.json`.  This will be `teams-manifest.json` for Teams.
   1. Manually update the manifest.json
      - Edit the `manifest.json` contained in the `/appManifest` folder
        - Replace with your AppId (that was created above) *everywhere* you see the place holder string `${{BOT_ID}}`
        - Replace `${{BOT_DOMAIN}}` with your Agent url.  For example, the tunnel host name.
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
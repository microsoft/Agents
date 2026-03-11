# MultiAgent Sample

This demonstrates an Agent that implements multiple AgentApplication instances.

- Two echo agents:  Agent1 and Agent2
- Requires two Azure Bots, each with a different Messaging Endpoints to the same host
  - Azure Bot 1: https://{host}/api/1/messages
  - Azure Bot 2: https://{host}/api/2/messages
- Both agents are added in Program.cs
  ```csharp
  builder.AddAgent<Agent1>();
  builder.AddAgent<Agent2>();
  ``` 
- Http endpoints are mapped for each agent
  - Automatically via `MapAgentApplicationEndpoints` in Program.cs
  - `/api/1/messages` -> `Agent1`
  - `/api/2/messages` -> `Agent2`

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your Azure Bot.
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your Agent to.
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

### QuickStart using WebChat

1. [Create an Azure Bot for Agent1](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. [Create an Azure Bot for Agent2](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connections in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "ServiceConnection1": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId1}}", // this is the Client ID used for the connection.
            "ClientSecret": "", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        },
        "ServiceConnection2": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId2}}", // this is the Client ID used for the connection.
            "ClientSecret": "", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      "ConnectionsMap": [
        {
          "Audience": "{{ClientId1}}",
          "Connection": "ServiceConnection1"
        },
        {
          "Audience": "{{ClientId2}}",
          "Connection": "ServiceConnection2"
        }
      ] 
      ```

      1. Replace all **{{ClientId1}}** with the AppId of the Azure Bot 1.
      1. Replace all **{{ClientId2}}** with the AppId of the Azure Bot 2.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **ClientSecret** to the Secret for each Connection.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. If you are using the AspNetExtensions from the FullAuthentication sample, update the "TokenValidation" section in appsettings
   ```json
   "TokenValidation": {
     "Audiences": [
       "{{ClientId1}}",
       "{{ClientId2}}"
     ],
     "TenantId": "{{TenantId}}"
   },
   ``` 

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On Azure Bot 1, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/1/messages`

1. On Azure Bot 2, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/2/messages`

1. Start the Agent in Visual Studio

1. Select **Test in WebChat** on either Azure Bot


## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
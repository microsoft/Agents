# FullAuthentication Sample

This is a sample of a simple Agent that is hosted on an Asp.net core web service, with JWT Token Validation.  This is the EmptyAgent, with AspNet JWT token authentication enabled.

## AspNet Authentication for an Agent
- This samples includes authentication setup for validating tokens from Entra or Azure Bot Service
  - The `AspNetExtensions.AddAgentAspNetAuthentication` supports validating either type of token
  - Performs audience, issuer, signing validation, and signing key refresh.
  - It also support configuration for other Azure clouds such as US Gov, Gallatin or others.
- If your agent will not be communicating with Azure Bot Service, any AspNet mechanism to authenticate requests can be used instead.

## Overview of adding AspNet Authentication to an Agent
1. Add the AspNet middleware to the Agent in `Program.cs`:
   ```csharp
   // Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
   // configured in the appsettings.json "TokenValidation" section.
   builder.Services.AddControllers();
   builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
   ```
1. Enable the AspNet authentication middleware in the Agent pipeline in `Program.cs`, after the line to build the app (`var app = builder.Build();`):
   ```csharp
   app.UseAuthentication();
   app.UseAuthorization();
   ```
1. Require authorization for the Agents incoming message endpoint in `Program.cs`:
   ```csharp
   app.MapPost(
      "/api/messages",
      async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
      {
          await adapter.ProcessAsync(request, response, agent, cancellationToken);
      })
      .RequireAuthorization();  // Require authorization for the incoming message endpoint
   ```
1. Configure the token validation settings in `appsettings.json`:
   ```json
   "TokenValidation": {
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   }
   ```
   - Replace **{{ClientId}}** with the Client ID of your Azure Bot or Entra application.
   - Replace **{{TenantId}}** with the Tenant Id where your application is registered.

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

## Getting Started with FullAuthentication Sample

Read more about [Running an Agent](../../../docs/HowTo/running-an-agent.md)

### QuickStart using WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Audiences": [
          "{{ClientId}}" // this is the Client ID used for the Azure Bot
        ],
        "TenantId": "{{TenantId}}" 
      },
   
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
      },
      ```

      1. Replace all **{{ClientId}}** with the AppId of the Azure Bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent in Visual Studio

1. Select **Test in WebChat** on the Azure Bot

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
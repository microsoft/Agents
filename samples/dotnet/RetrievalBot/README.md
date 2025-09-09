# RetrievalBot Sample with Semantic Kernel

This is a sample of a simple Retrieval Agent that is hosted on an Asp.net core web service.  This Agent is configured to accept a request asking for information about Build sessions by Contoso and respond to the caller with an Adaptive Card.

This Agent Sample is intended to introduce you to the Copilot Retrieval API Grounding capabilities. It uses Semantic Kernel with the Microsoft 365 Agents SDK. It is a great example to understand the basics of Microsoft 365 Agents SDK.

***Note:*** This sample requires JSON output from the model which works best from newer versions of the model such as gpt-4o-mini.

## Prerequisites

- [.NET](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [Dev Tunnels](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started)
- Download and install Visual Studio (I have 2022 version).
- You need Azure subscription to create Azure Bot Service. Follow the steps here – Link TBD
- Have Git available on your computer [Git - Installing Git](https://git-scm.com/book/en/v2/Getting-Started-Installing-Git)
- You also need Copilot licenses enabled in your tenant for calling the Retrieval API. And actually deploying the Agent to Copilot
- If you have a Copilot tenant, make sure your admin can install the app package from MAC (admin.microsoft.com). This requires admin level access and is the only way to upload Agentic applications to Copilot.
- If you do not want or can’t get a Copilot tenant, but have a Dev Tenant, you can still use this sample and deploy your Agent to your Teams channel or chat or meeting. Here are the steps for this - [Upload your custom app - Teams | Microsoft Learn](https://learn.microsoft.com/microsoftteams/platform/concepts/deploy-and-publish/apps-upload). This process doesn’t require Admin level access. Just ensure that your admin has allowed users to upload apps to Teams store. [Manage custom app policies and settings - Microsoft Teams | Microsoft Learn](https://learn.microsoft.com/microsoftteams/teams-custom-app-policies-and-settings).
- You will not be able to use the Copilot Retrieval APIs if you don't have a Copilot Tenant.
- You also need to be a SharePoint administrator and should be able to create a SPO site and add a sample document from which you want to retrieve relevant information using the Copilot Retrieval API. Once you upload your document(s), give the API a couple of hours to index so that it can return relevant information. You can upload the document 'Contoso sessions at Microsoft Build Conference 2025.docx' in SharePoint Documents folder to ask it the sample queries listed below.


## Running this sample

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to preform the following tasks:
    - Create and configure [Entra ID Application Identities](https://aka.ms/AgentsSDK-CreateBot)
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

    1. Configure your AI service settings. The sample provides configuration placeholders for using Azure OpenAI or OpenAI, but others can be used as well.
    1. With Azure OpenAI:
        1. With Credential Free (Keyless):
        
           This is a secure way to authenticate to Azure resources without needing to store credentials in your code. Your Azure user account is assigned the "Cognitive Services OpenAI User" role, which allows you to access the OpenAI resource.
           Follow this guide [Role-based access control for Azure resources](https://learn.microsoft.com/azure/ai-services/openai/how-to/role-based-access-control) to assign the "Cognitive Services OpenAI User" role to your Azure user account and Managed Identities.    
           
           Then you just need to configure Azure OpenAI Endpoint and DeploymentName in the appsettings.json file
        
        1. With dotnet user-secrets (for running locally)
            1. From a terminal or command prompt, navigate to the root of the sample project.
            1. Run the following commands to set the Azure OpenAI settings:
				```bash
				dotnet user-secrets set "AIServices:AzureOpenAI:ApiKey" "<YOUR_AZURE_OPENAI_API_KEY>"
                dotnet user-secrets set "AIServices:AzureOpenAI:Endpoint" "<YOUR_AZURE_OPENAI_ENDPOINT>"
                dotnet user-secrets set "AIServices:AzureOpenAI:DeploymentName" "<YOUR_AZURE_OPENAI_DEPLOYMENT_NAME>"
                dotnet user-secrets set "AIServices:UseAzureOpenAI" true
                ```
        1. With environment variables (for deployment)
            1. Set the following environment variables:
                1. `AIServices__AzureOpenAI__ApiKey` - Your Azure OpenAI API key
                1. `AIServices__AzureOpenAI__Endpoint` - Your Azure OpenAI endpoint
                1. `AIServices__AzureOpenAI__DeploymentName` - Your Azure OpenAI deployment name
                1. `AIServices__UseAzureOpenAI` - `true`
    1. With OpenAI:
		1. With dotnet user-secrets (for running locally)
			1. From a terminal or command prompt, navigate to the root of the sample project.
			1. Run the following commands to set the OpenAI settings:
               ```bash
				dotnet user-secrets set "AIServices:OpenAI:ModelId" "<YOUR_OPENAI_MODEL_ID_>"
                dotnet user-secrets set "AIServices:OpenAI:ApiKey" "<YOUR_OPENAI_API_KEY_>"
                dotnet user-secrets set "AIServices:UseAzureOpenAI" false
                ```
        1. With environment variables (for deployment)
            1. Set the following environment variables:
                1. `AIServices__OpenAI__ModelId` - Your OpenAI model ID
                1. `AIServices__OpenAI__ApiKey` - Your OpenAI API key
                1. `AIServices__UseAzureOpenAI` - `false`

### QuickStart using WebChat

1. Create an Azure Bot
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Enabled": true,
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
              "ClientId": "00000000-0000-0000-0000-000000000000", // this is the Client ID used for the connection.
              "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
              "Scopes": [
                "https://api.botframework.com/.default"
              ]
          }
      }
      ```

      1. Set the **ClientId** to the AppId of the bot identity.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      1. Set the **TenantId** to the Tenant Id where your application is registered.
      1. Set the **Audience** to the AppId of the bot identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

2. One last thing before we run our agent app. Go to Plugins/BuildRetrievalPlugin.cs and udpate the FilterExpression
3. Start the Agent in Visual Studio
4. Select **Test in WebChat** on the Azure Bot


## Sample queries to try with this bot
1. Hey there!
2. Can you give me a snapshot of all the sessions that Contoso is doing at Build 2025?
3. How many days till Build 2025?
4. I haven't seen a demo for the Pricing Analytics session. Can you send a mail to Adele Vance requesting for a Demo run this Friday? 


## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
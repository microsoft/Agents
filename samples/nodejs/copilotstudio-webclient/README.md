# Copilot Studio WebClient

This is a sample to show how to use the `@microsoft/agents-copilotstudio-client` package to talk to an Agent hosted in CopilotStudio using WebChat.

## Prerequisite

1. An Agent Created in Microsoft Copilot Studio or access to an existing Agent.
2. Ability to configure a local web server to serve the files in the `web` folder. If you are using VSCode you can install the extension [Live Server](https://marketplace.visualstudio.com/items?itemName=ritwickdey.LiveServer)
3. Ability to Create an Application Identity in Azure for a Public Client/Native App Registration Or access to an existing Public Client/Native App registration with the `CopilotStudio.Copilots.Invoke` API Permission assigned. 

## Authentication

The Copilot Studio Client requires a User Token to operate. For this sample, we are using a user interactive flow to get the user token for the application ID created above. Other flows are allowed.

## Create an Agent in Copilot Studio

1. Create an Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
    1. Publish your newly created Copilot
    2. Goto Settings => Advanced => Metadata and copy the following values, You will need them later:
        1. Schema name
        2. Environment Id

## Create an Application Registration in Entra ID

This step will require permissions to create application identities in your Azure tenant. For this sample, you will create a Client Application Identity, which does not have secrets.

1. Open https://portal.azure.com 
2. Navigate to Entra Id
3. Create a new App Registration in Entra ID 
    1. Provide a Name
    2. Choose "Accounts in this organization directory only"
    3. In the "Select a Platform" list, Choose "Single-page Application"
    4. In the Redirect URI url box, type in the URL you will use to serve this application, for local development use `http://localhost:5500` (**note: use the port used by your local web server**)
    5. Then click register.
4. In your newly created application
    1. On the Overview page, Note down for use later when configuring the example application:
        1. The Application (client) ID
        2. The Directory (tenant) ID
    2. Go to API Permissions in `Manage` section
    3. Click Add Permission
        1. In the side panel that appears, Click the tab `API's my organization uses`
        2. Search for `Power Platform API`.
            1. *If you do not see `Power Platform API` see the note at the bottom of this section.*
        3. In the *Delegated permissions* list, choose `CopilotStudio` and Check `CopilotStudio.Copilots.Invoke`
        4. Click `Add Permissions`
    4. (Optional) Click `Grant Admin consent for copilotsdk`

> [!TIP]
> If you do not see `Power Platform API` in the list of API's your organization uses, you need to add the Power Platform API to your tenant. To do that, goto [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow the instructions on Step 2 to add the Power Platform Admin API to your Tenant

## Instructions - Configure the Example Application

With the above information, you can now configure the web client in the `web` folder.

1. Open the `settings.TEMPLATE.js` file and rename it to `settings.js`.
2. Configure the values based on what was recorded during the setup phase.

```bash
  environmentId="" # Environment ID of environment with the CopilotStudio App.
  schemaName="" # Schema Name of the Copilot to use
  tenantId="" # Tenant ID of the App Registration used to login, this should be in the same tenant as the Copilot.
  appClientId="" # App ID of the App Registration used to login, this should be in the same tenant as the CopilotStudio environment.
  # Alternatively, you can provide a direct URL to connect to Copilot Studio instead of specifying the `environmentId` and `schemaName` values:
 directConnectUrl="" # The URL to connect to the Copilot Studio service. If set, overrides `environmentId` and `schemaName`.
```

#### Optional Configuration

This sample lets you configure the following settings in the .env file:
```bash
authorityEndpoint="" # The login authority to use for the connection. Default: "https://login.microsoftonline.com".
cloud="" # The cloud hosting the Power Platform Services. Default: "Prod".
customPowerPlatformCloud="" # The Power Platform API endpoint when cloud is set to "Other".
copilotAgentType="" # The type of Copilot Studio Agent (Published or Prebuilt). Default: "Published".
useExperimentalEndpoint="" # The flag to use the URL provided via the  "x-ms-d2e-experimental" header for subsequent calls to the Copilot Studio service.
```

3. Use a web server to serve the files in the `web` folder. If you are using [Live Server](https://marketplace.visualstudio.com/items?itemName=ritwickdey.LiveServer), right click `index.html` and select `Open with Live Server`

4. Open a browser and navigate to the URL exposed by Live Server, by default it should be `http://localhost:5500`. Make sure it's using the same port as the one used in your App Registration.

5. You might need to allow pop-up window in your browser

6. In the pop-up window, authenticate with your user credentials.

7. You should see a Web Page with WebChat component to talk with your agent.

# OBO Auth Sample

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-net), it shows how to use authorization in your Agent using OAuth and OBO.

- The sample uses the Agent SDK User Authorization capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop an Agent that authorizes users with various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.
- This sample shows how to use an OBO Exchange to communicate with Microsoft Copilot Studio using the [CopilotClient class](https://learn.microsoft.com/python/api/microsoft-agents-copilotstudio-client/microsoft_agents.copilotstudio.client.copilotclient).

## Prerequisites

- [Python](https://www.python.org/) version 3.9 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)
- Access to CopilotStudio to [create an Agent](https://learn.microsoft.com/microsoft-copilot-studio/fundamentals-get-started?tabs=web)

## Local Setup

### Configuration

1. Create an Agent in Copilot Studio.
   1. Publish your newly created Agent
   1. Got to Settings => Advanced => Metadata and copy the following values. You will need them later:
      1. Schema name
      1. Environment ID

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
   1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
   2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
   3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.

1. Setting up OAuth for an exchangeable token 
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
         1. Create a new secret and record the value. This will be used later.

1. Create Azure Bot **OAuth Connection**
   1. On the Azure Bot created in Step #2, Click **Configuration** tab then the **Add OAuth Connection Settings** button.
   1. Enter a **Name**.  This will be used later.
   1. For **Service Provider** select **Azure Active Directory v2**
   1. **Client id** and **Client Secret** are the values created in step #4.
   1. Enter the **Tenant ID**
   1. **Scopes** is `api://botid-{{appid}}/defaultScope`. appid is the **Client id** value from #4.

1. Configure the authorization handlers
   1. Open the `.env` file and update:
   1. Set the **CONNECTIONS__MCS__SETTINGS__CLIENTID** to the **Client id** from #4
   1. Set the **CONNECTIONS__MCS__SETTINGS__CLIENTSECRET** to the **Client Secret** value from #4.
   1. Set the **CONNECTIONS__MCS__SETTINGS__TENANTID** to the Tenant Id.
   1. Keep **AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__MCS__SETTINGS__OBOCONNECTIONNAME=MCS**
   1. Set **AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__MCS__SETTINGS__AZUREBOTOAUTHCONNECTIONNAME={{Name}}** where **Name** is the name of your OAuth connection created in step 5.

1. Configure the sample to use your CopilotStudio Agent by modifying the following variables in the `.env` file based on the values obtained in step #1:

```bash
  COPILOTSTUDIOAGENT__ENVIRONMENTID="" # Environment ID of the Agent from MCS
  COPILOTSTUDIOAGENT__SCHEMANAME="" # Schema Name of the Agent from MCS
```   

1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Take note of the url shown after `Connect via browser:`

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

### Running the Agent

1. Open this folder from your IDE or Terminal of preference
1. (Optional but recommended) Set up virtual environment and activate it.
1. Install dependencies

```sh
pip install -r requirements.txt
```

### Run in localhost, anonymous mode

1. Start the application

```sh
python -m src.main
```

At this point you should see the message 

```text
======== Running on http://localhost:3978 ========
```

The agent is ready to accept messages.

## Accessing the Agent

### Using the Agent in WebChat

1. Go to your Azure Bot Service resource in the Azure Portal and select **Test in WebChat**

## Further reading

To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

For more information on logging configuration, see the logging section in the Quickstart Agent sample README.
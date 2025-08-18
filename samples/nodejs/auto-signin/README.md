# AutoSignIn

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-js), it shows how to use Auto SignIn user authorization in your Agent.

This sample uses different routes, and some are configured to use one or more auth handlers:

```ts
  this.onMessage('/logout', this._logout)
  this.onMessage('/me', this._profileRequest, ['graph'])
  this.onMessage('/prs', this._pullRequests, ['github'])
  this.onMessage('/status', this._status, ['graph', 'github'])
```


The sample uses the bot OAuth capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop a bot that authorizes users to various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.

- ## Prerequisites

-  [NodeJS](https://nodejs.org) version 20.0 or greater
-  [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)

## Configure Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. [Add OAuth to your bot](https://aka.ms/AgentsSDK-AddAuth) using the _Azure Active Directory v2_ Provider.

1. Create a second Azure Bot **OAuth Connection** using the _GitHub_ provider.

  > To configure OAuth for GitHub you need a GitHub account, under settings/developer settings/OAuth apps, create a new OAuth app, and set the callback URL to `https://token.botframework.com/.auth/web/redirect`. Then you will need to provide the clientId and clientSecret, and the required scopes: `user repo`

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [MSAL Authentication provider](https://learn.microsoft.com/microsoft-365/agents-sdk/azure-bot-authentication-for-javascript)

  1. Open the `env.TEMAPLTE` file in the root of the sample project and rename it to `.env`
  1. Update **clientId**, **tenantId** and **clientSecret**
  

1. Configure the UserAuthorization handlers
   1. Open the `.env` file and add the name of the OAuth Connections, note the prefix must match the name of the auth handlers in the code, so for:

    ```ts
    class AutoSignInDemo extends AgentApplication<TurnState> {
      constructor () {
        super({
          storage: new MemoryStorage(),
          authorization: {
            graph: { text: 'Sign in with Microsoft Graph', title: 'Graph Sign In' },
            github: { text: 'Sign in with GitHub', title: 'GitHub Sign In' },
          }
        })
    ```

    you should have one item for `graph` and aonther for `github`

    ```env
    graph_connectionName=
    github_connectionName=
    ```
      

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Update your Azure Bot ``Messaging endpoint`` with the tunnel Url:  `{tunnel-url}/api/messages`

1. Run the bot from a terminal with `npm start`

1. Test via "Test in WebChat"" on your Azure Bot in the Azure Portal.

<!-- ## Running this Agent in Teams

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

1. Select **Preview in Teams** in the upper right corner -->

## Interacting with the Agent

- When the conversation starts, you will be greeted with a welcome message, and another message informing the token status. 
- Sending `/me` will trigger the OAuth flow and display additional information about you.
- Note that if running this in Teams and SSO is setup, you shouldn't see any "sign in" prompts.  This is true in this sample since we are only requesting a basic set of scopes that Teams doesn't require additional consent for.

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.


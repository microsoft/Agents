# Auto Sign-In Sample

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-python), it shows how to use Auto SignIn user authorization in your Agent.

This sample uses different routes, and some are configured to use an auth handler (more than one handler can be applied to a route). Below is an abbreviate version of the decorators used to configure the routes, which are really commands to the Agent:

```python
  @AGENT_APP.message("/status")
  @AGENT_APP.message("/logout")
  @AGENT_APP.message("/me", auth_handlers=["GRAPH"])
  @AGENT_APP.message("/prs", auth_handlers=["GITHUB"])
```

## Prerequisites

-  [Python](https://www.python.org/) version 3.9 or higher
-  [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) (for local development)

## Local Setup

### Configure Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID** to the AppId of the bot identity.
      2. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET** to the Secret that was created for your identity. *This is the `Secret Value` shown in the AppRegistration*.
      3. Set the **CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID** to the Tenant Id where your application is registered.

1. [Add OAuth to your bot](https://aka.ms/AgentsSDK-AddAuth) using the _Azure Active Directory v2_ Provider.

1. Create a second Azure Bot **OAuth Connection** using the _GitHub_ provider.

  > To configure OAuth for GitHub you need a GitHub account, under settings/developer settings/OAuth apps, create a new OAuth app, and set the callback URL to `https://token.botframework.com/.auth/web/redirect`. Then you will need to provide the clientId and clientSecret, and the required scopes: `user repo`

1. Configure the authorization handlers
   1. Open the `.env` file and add the name of the OAuth Connections, note the prefix must match the name of the auth handlers in the code, so for:

    ```python
    @AGENT_APP.message("prs", auth_handlers=["GITHUB"])
    @AGENT_APP.message("me", auth_handlers=["GRAPH"])
    ```

    you should have one pair of items for `GRAPH` and aonther for `GITHUB`

    ```env
      AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__GRAPH__SETTINGS__AZUREBOTOAUTHCONNECTIONNAME=
      AGENTAPPLICATION__USERAUTHORIZATION__HANDLERS__GITHUB__SETTINGS__AZUREBOTOAUTHCONNECTIONNAME=
    ```

    In this sample, these are the only auth handlers.

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

1. When the conversation starts, you will be greeted with a welcome message, and another message informing the token status. 
1. Sending `/me` will trigger the OAuth flow and display additional information about you.
1. Note that if running this in Teams and SSO is setup, you shouldn't see any "sign in" prompts.  This is true in this sample since we are only requesting a basic set of scopes that Teams doesn't require additional consent for.

### Using the Agent in Teams
For Auto Sign-In in Teams, additional steps are needed to [Enable SSO for a Teams app](https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/authentication/bot-sso-overview)

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.


For more information on logging configuration, see the logging section in the Quickstart Agent sample README.

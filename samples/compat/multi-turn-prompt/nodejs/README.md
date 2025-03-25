# Multi-turn prompt

This bot has been created with Agents SDK to show how to use the prompts classes included in `agents-bot-hosting-dialogs`.  This bot will ask for the user's name and age, then store the responses. It demonstrates a multi-turn dialog flow using a text prompt, a number prompt, and state accessors to store and retrieve values.

## Prerequisites

- [Node.js](https://nodejs.org) version 20 or higher

    ```bash
    # determine node version
    node --version
    ```


## Running this sample

1. Open this folder from your IDE or Terminal of preference
1. Install dependencies

```bash
npm install
```

### Run in localhost, anonymous mode

1. Create the `.env` file (or rename env.TEMPLATE)

```bash
cp env.TEMPLATE .env
```

1. Start the application

```bash
npm start
```

At this point you should see the message 

```text
Server listening to port 3978 for appId  debug undefined
```

The bot is ready to accept messages.

### Interact with the bot from the Teams App Test Tool

To interact with the bot you need a chat client, during the install phase we have acquired the `teams-test-app-tool` than can be used to interact with your bot running in `localhost:3978`

1. Start the test tool with 

```bash
npm run test-tool
```

The tool will open a web browser showing the Teams App Test Tool, ready to send messages to your bot.

Alternatively you can run the next command to start the bot and the test tool with a single command (make sure you stop the bot started previously):

```bash
npm test
```

Refresh the browser to start a new conversation with the Multi-turn-prompt bot.

You should start the conversation by sending an initial message to the bot, and the dialog flow will start.


### Interact with the bot from WebChat using Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below
  
1. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **clientId** to the AppId of the bot identity.
      2. Set the **clientSecret** to the Secret that was created for your identity.
      3. Set the **tenantId** to the Tenant Id where your application is registered.

1. Install the tool [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)   
1. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Take note of the url shown after `Connect via browser:`

4. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

5. Start the Agent using `npm start`

6. Select **Test in WebChat** on the Azure portal.


### Deploy to Azure

[TBD]


## Further reading

To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

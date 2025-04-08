# Cards-Agent

This is a sample of a simple Agent that is hosted on an Node.js web service.  This Agent is configured to show how to create an agent that uses rich cards to enhance your conversation design.

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
Server listening to port 3978 for appId debug undefined
```

The Agent is ready to accept messages.

### Interact with the Agent from the Teams App Test Tool

To interact with the agent you need a chat client, during the install phase we have acquired the `teams-test-app-tool` than can be used to interact with your agent running in `localhost:3978`

1. Start the test tool with 

```bash
npm run test-tool
```

The tool will open a web browser showing the Teams App Test Tool, ready to send messages to your agent.

Alternatively you can run the next command to start the agent and the test tool with a single command (make sure you stop the agent started previously):

```bash
npm test
```

Refresh the browser to start a new conversation with the Cards agent.

You should see a message with the list of available cards in Agents:
- Adaptive Card
- Animation Card
- Audio Card
- Hero Card
- Receipt Card
- O365 Connector Card
- Thumbnail Card
- Video Card

### Interact with the agent from WebChat UI using Azure Bot Service

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below
  
2. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **clientId** to the AppId of the agent identity.
      2. Set the **clientSecret** to the Secret that was created for your identity.
      3. Set the **tenantId** to the Tenant Id where your application is registered.

3. Install the tool [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)   
4. Run `dev tunnels`. See [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

5. Take note of the url shown after `Connect via browser:`

6. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

7. Start the Agent using `npm start`

8. Select **Test in WebChat** on the Azure portal.

### Deploy to Azure

[TBD]

## Further reading

To learn more about building  Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

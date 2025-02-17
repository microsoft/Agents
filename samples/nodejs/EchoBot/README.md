# Echo-bot

This is a sample of a simple Agent that is hosted on an Node.js web service.  This Agent is configured to accept a request and echo the text of the request back to the caller.

This Agent Sample is intended to introduce you to the basic operation of the Microsoft 365 Agents SDK messaging loop. It can also be used as the base for a custom Agent you choose to develop.

## Prerequisites

- [Node.js](https://nodejs.org) version 18 or higher

    ```bash
    # determine node version
    node --version
    ```
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases) for Testing Web Chat.

## Running this sample

**To run the sample connected to Azure Bot Service, the following additional tools are required:**

- Access to an Azure Subscription with access to perform the following tasks:
    - Create and configure Entra ID Application Identities
    - Create and configure an [Azure Bot Service](https://aka.ms/AgentsSDK-CreateBot) for your bot
    - Create and configure an [Azure App Service](https://learn.microsoft.com/azure/app-service/) to deploy your bot on to.
    - A tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to an external client such as Microsoft Teams.

### QuickStart using Bot Framework Emulator

1. Open the Echobot Sample
2. Install the modules using `npm install`
3. Run the bot with the command `npm start`
4. An application will listen for requests on port `https://localhost:65349/`.
5. Open the [BotFramework Emulator](https://github.com/Microsoft/BotFramework-Emulator/releases)
    1. Click **Open Bot**
    2. In the bot URL field, input the URL you noted down from the web page and add /api/messages to it. It should appear similar to `https://localhost:65349/api/messages`
    3. Click **Connect**

If all is working correctly, the Bot Emulator should show you a Web Chat experience with the words **"Hello and Welcome!"**

If you type a message and hit enter, or the send arrow, your messages should be returned to you with **Echo:your message**

### QuickStart using WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below
  
2. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project and configure the following values:
      1. Set the **ClientId** to the AppId of the bot identity.
      2. Set the **ClientSecret** to the Secret that was created for your identity.
      3. Set the **TenantId** to the Tenant Id where your application is registered.
   
3. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

5. Start the Agent in Visual Studio

6. Select **Test in WebChat** on the Azure Bot

## Further reading

To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

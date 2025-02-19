# Echo-bot

This is a sample of a simple Agent that is hosted on an Node.js web service.  This Agent is configured to accept a request and echo the text of the request back to the caller.

This Agent Sample is intended to introduce you to the basic operation of the Microsoft 365 Agents SDK messaging loop. It can also be used as the base for a custom Agent you choose to develop.

## Prerequisites

- [Node.js](https://nodejs.org) version 20 or higher

    ```bash
    # determine node version
    node --version
    ```
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

### QuickStart using Teams test app tool

1. Open the Echobot Sample
2. Install the packages using `npm install`
3. Rename the `env.TEMPLATE` file to `.env`
4. Run the command `npm run test` to start the bot and the Teams test app tool 

If all is working correctly, the tool should show you a Web Chat experience with the words **"Hello and Welcome!"**

If you type a message and hit enter, or the send arrow, your messages should be returned to you with **Echo:your message**

### QuickStart using WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below
  
2. Configuring the token connection in the Agent settings
    1. Open the `env.TEMPLATE` file in the root of the sample project, rename it to `.env` and configure the following values:
      1. Set the **ClientId** to the AppId of the bot identity.
      2. Set the **ClientSecret** to the Secret that was created for your identity.
      3. Set the **TenantId** to the Tenant Id where your application is registered.
   
3. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

4. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

5. Start the Agent using `npm start`

6. Select **Test in WebChat** on the Azure Bot

## Further reading

To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

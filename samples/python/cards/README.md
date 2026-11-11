# Cards Sample

This sample hosts a simple agent on a Python web service. It demonstrates how to use rich cards to enhance a conversation.

The agent supports these cards:

1. Adaptive Card
2. Animation Card
3. Audio Card
4. Hero Card
5. Receipt Card
6. Thumbnail Card
7. Video Card

## Prerequisites

- [Python](https://www.python.org/) version 3.10 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for Azure Bot Service testing

## Local setup

1. Open this folder in your preferred IDE or terminal.
1. Optionally create and activate a virtual environment.
1. Install dependencies:

   ```bash
   pip install -r requirements.txt
   ```

1. Create `.env` from the template and configure it as needed for your client:

   ```bash
   cp env.TEMPLATE .env
   ```

1. Start the application:

   ```bash
   python -m src.main
   ```

The agent listens at `http://localhost:3978` and is ready to accept messages. Select a card from the menu, send a number from `1` through `7`, or send `display card options` to show the menu again.

## Test with Azure Bot Service WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot) and record its Application ID, Tenant ID, and client secret.
1. Copy `env.TEMPLATE` to `.env` and configure `CLIENTID`, `CLIENTSECRET`, and `TENANTID`.
1. Host a dev tunnel with anonymous tunnel access:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. In the Azure Bot resource, select **Settings**, then **Configuration**, and set the messaging endpoint to `{tunnel-url}/api/messages`.
1. Start the agent with `python -m src.main`.
1. Select **Test in WebChat** in the Azure portal.

## Further reading

To learn more about building agents, see the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repository.

For more information about logging configuration, see the logging section in the Quickstart Agent sample README.

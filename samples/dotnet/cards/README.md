# Cards Sample

This sample hosts a simple agent on an ASP.NET Core web service. It demonstrates how to use rich cards to enhance a conversation.

The agent supports these cards:

1. Adaptive Card
2. Animation Card
3. Audio Card
4. Hero Card
5. Receipt Card
6. Thumbnail Card
7. Video Card

## Prerequisites

- [.NET](https://dotnet.microsoft.com/download/dotnet/8.0) version 8.0 or higher
- [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) for Azure Bot Service testing

## Local setup

1. Open this folder in your preferred IDE or terminal.
1. Configure the placeholders in `appsettings.json` for the client you will use.
1. Start the application:

   ```bash
   dotnet run
   ```

The agent listens at `http://localhost:3978` and is ready to accept messages. Select a card from the menu, send a number from `1` through `7`, or send `display card options` to show the menu again.

## Test with Azure Bot Service WebChat

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot) and record its Application ID, Tenant ID, and client secret.
1. Configure `ClientId`, `ClientSecret`, `TenantId`, and the token-validation audience in `appsettings.json`. For local secrets, use an ignored `appsettings.Development.json` file.
1. Host a dev tunnel with anonymous tunnel access:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. In the Azure Bot resource, select **Settings**, then **Configuration**, and set the messaging endpoint to `{tunnel-url}/api/messages`.
1. Start the agent with `dotnet run`.
1. Select **Test in WebChat** in the Azure portal.

## Further reading

To learn more about building agents, see the [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repository.

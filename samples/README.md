# M365 Agents SDK Samples

Here you can find samples showing how to use the Agents SDK in different languages. 


## Samples list

|Category | Name | Description | node | dotnet | python |
|---------|-------------|-------------|--------|--------|--------|
| Basic   | Echo Bot | Simplest bot | [basic/echo-bot/nodejs](./basic/echo-bot/nodejs) | [basic/echo-bot/dotnet](./basic/echo-bot/dotnet) | TBD |
| Basic   | Copilot Studio Client | Consume CopilotStudio Agent | [basic/copilotstudio-client/nodejs](./basic/copilotstudio-client/nodejs) | TBD | TBD |
| Complex | Copilot Studio Skill | Call the echo bot from a Copilot Studio skill | [complex/copilotstudio-skill/nodejs](./complex/copilotstudio-skill/nodejs) | TBD | TBD |


## Debugging in localhost

To debug your Agent in `localhost` you can:

1. Use a client emulator, such as the [Teams App Test Tool](https://learn.microsoft.com/en-us/microsoftteams/platform/toolkit/debug-your-teams-app-test-tool), or the [BotFramework Emulator](https://learn.microsoft.com/en-us/azure/bot-service/bot-service-debug-emulator)
1. Use the WebChat UI, by registering one instance of Azure Bot Service, and configure the endpoint to use a tunnel to localhost (eg, by using devtunnels)

### How to register an ABS instance with DevTunnels

1. Install devtunnels and the Azure CLI
  1. This repo contains a CodeSpaces configuration with all the required tools already installed.
1. Login into devtunnels: `devtunnel user login`.
1. Login into Azure: `az login`.
1. Run the script `_tools/configure-abs-tunnel.sh`.
  1. This script configures devtunnels and ABS, using the machine name, and produces a `.env` file.
1. Copy the generated `<your-machine>.env` file to the sample folder with file name `.env`.
1. Start the tunnel before debugging `devtunnel host <tunnel_id>`

> [!Tip]
> You can use the same ABS instance and tunnel to debug any of these samples, with the only caveat that you can only use one at a time.
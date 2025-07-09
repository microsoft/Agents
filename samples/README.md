# M365 Agents SDK Samples

To enable debugging, open this folder using Visual Studio Code (VSCode). Follow these steps:

1. Launch VSCode.
2. Select `File` > `Open Folder...`.
3. Navigate to the `/workspaces/Agents/samples` directory and open it.
4. Once the folder is open, you can start debugging by setting breakpoints and running the samples.

## Samples list

|Category | Name | Description | node | dotnet | python |
|---------|-------------|-------------|--------|--------|--------|
| Basic   | Empty Agent | Simplest agent | [basic/empty-agent/nodejs](./basic/empty-agent/nodejs) | [basic/empty-agent/dotnet](./basic/empty-agent/dotnet) | TBD |
| Basic   |  Full Authentication | Empty Agent w/JWT Auth | TBD | [basic/full-authentication/dotnet](./basic/empty-agent/dotnet) | TBD |
| Basic   | AutoSignIn | Simple OAuth agent using Graph | TBD | [basic/authorization/auto-signin/dotnet](./basic/authorization/auto-signin/dotnet) | TBD |
| Basic   | OBOAuthorization | OBO to Copilot Studio Agent | TBD | [basic/authorization/obo-authorization/dotnet](./basic/authorization/obo-authorization/dotnet) | TBD |
| Basic   | Weather Agent | WeatherAgent with various orchestrator libs | (Using LangChain.js) [basic/weather-agent/nodejs](./basic/weather-agent/nodejs) | (Using Semantic Kernel) [basic/weather-agent/dotnet](./basic/weather-agent/dotnet) | TBD |
| Basic   |  Streaming Agent | Streams OpenAI responses | TBD | [basic/azureai-streaming-poem-agent/dotnet](./basic/azureai-streaming-poem-agent/dotnet) | TBD |
| Basic   | Copilot Studio Client | Console app to consume a Copilot Studio Agent | [basic/copilotstudio-client/nodejs](./basic/copilotstudio-client/nodejs) | [basic/copilotstudio-client/dotnet](./basic/copilotstudio-client/dotnet) | TBD |
| Basic   | Copilot Studio Client - React Webchat | App to consume a Copilot Studio Agent integrated with a Webchat Client | [basic/copilotstudio-webchat-react](./basic/copilotstudio-webchat-react) | TBD | TBD |
| Complex | Copilot Studio Skill | Call the echo bot from a Copilot Studio skill | [complex/copilotstudio-skill/nodejs](./complex/copilotstudio-skill/nodejs) | [complex/copilotstudio-skill/dotnet](./complex/copilotstudio-skill/dotnet) | TBD |

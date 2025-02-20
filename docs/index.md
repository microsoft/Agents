# Microsoft 365 Agents SDK

The Microsoft 365 Agent SDK simplifies building full stack, multichannel, trusted agents for platforms including M365, Teams, Copilot Studio, and Webchat. We also offer integrations with 3rd parties such as Facebook Messenger, Slack, or Twilio. The SDK provides developers with the building blocks to create agents that handle user interactions, orchestrate requests, reason responses, and collaborate with other agents.

The M365 Agent SDK is a comprehensive framework for building enterprise-grade agents, enabling developers to integrate components from the Azure AI SDK, Semantic Kernel, as well as AI components from other vendors.

Please note, you may find references to Bot Framework throughout the code and the existing documentation, that includes legacy functionality being transitioned out of the SDK. We will be updating the documentation over time and will post updated content here (and also on Microsoft Learn) 

Below you can find some key documentation to get started with the Agents SDK

## How can developers get started with the Agents SDK?

The Microsoft 365 Agents SDK is currently in public preview, and developers can get started by taking a look at the samples and reading our initial set of docs here.

There is a core set of documentation available that provides more details on the functionality and capabilities of the SDK, and also walks developers through the core components: 

## Start here
- [What is the Agent SDK](./HowTo/what-is-the-agents-sdk.md)
- [Deeper dive on how an agent works](./HowTo/how-the-agents-sdk-works.md)
- [Echo Agent/Bot Sample Breakdown](./HowTo/echo-agent-sample-breakdown.md)

## Running and configuring an Agent
- [Running an Agent](./HowTo/running-an-agent.md) 
- [Configuring OAuth for an Agent](./HowTo/azurebot-user-authentication-fic.md)

## Additional guidance
- [Creating an Azure Bot - Single Tenant](./HowTo/azurebot-create-single-secret.md) 
- [Creating an Azure Bot - Managed Identity](./HowTo/azurebot-create-msi.md) 
- [DotNet Agents SDK - MSAL Configuration](./HowTo/MSALAuthConfigurationOptions.md)
- [Agents in SharePoint Embedded SDK - with React Sample App](https://aka.ms/start-spe-copilot)
- [Configure authentication in JavaScript](./HowTo/azurebot-auth-for-js.md)

## Samples

Samples are the best way to get started with learning about the Agents SDK. The [C# Samples are in the C# repo](https://github.com/microsoft/Agents-for-net/tree/main/src/samples). 


| No. | Sample Name                        | Feature                            | Level        | Description                                                                                                                                                                                                                                               |
|----|------------------------------------|------------------------------------|--------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1   | Echo Agent/Bot                 | Microsoft 365 Agents SDK                        | Basic        | This is the best sample to start with to learn the basics of the Agents SDK and work through the various steps from local testing to deploying the agent in Azure. No additional services needed, and also no editing required in the code. |                                                                        |
| 2   | Semantic Kernel                    | Microsoft 365 Agents SDK                         | Basic        | Get started with the Agents SDK together with Semantic Kernel.   |                                                      
| 3   | Conversation Bot                    | Microsoft 365 Agents SDK                         | Basic        | This sample shows how to incorporate basic conversational flow into a Teams application. It also illustrates a few of the Teams-specific calls you can make from your bot. |                                                              
| 4   | Copilot Studio Skill                    | Microsoft 365 Agents SDK                         | Intermediate        | Get started with creating a skill that can be referenced from Microsoft Copilot Studio. |
| 5   | Multi-Agent with Copilot Studio                  | Microsoft 365 Agents SDK                         | Intermediate        | Learn the basics of connecting multiple agents together that are created with different services, including Microsoft Copilot Studio. |
| 6   | Messaging Extensions Search          | Messaging Extension                | Intermediate | This sample demonstrates how to use search-based Messaging Extensions with the Copilot SDK. It enables users to search for and select items via the command bar, supporting various interactions such as querying data and selecting results for further actions.  |                                                                   
| 7   | Adaptive Card Actions                | Adaptive Cards                     | Intermediate | This sample demonstrates how to implement different Adaptive Card action types using the Copilot SDK in Microsoft Teams. |
| 8   | Teams Authentication				| Authentication using OAuthPrompt  | Intermediate | This sample demonstrates user authentication in a Microsoft Teams bot, integrating OAuth tailored for Teams' unique authentication flow. Unlike other channels, Teams uses an Invoke Activity for authentication, which requires forwarding to the dialog with OAuthPrompt. A customizable TeamsActivityHandler, extending ActivityHandler, supports this flow seamlessly. |
| 9   | Teams SSO							| SSO & Graph Integration | Advanced     | This sample demonstrates how to integrate Azure AD authentication in Microsoft Teams using a bot with Single Sign-On (SSO) capabilities. Built with the Copilot SDK, it showcases OAuth SSO, Adaptive Cards, and Microsoft Graph API interactions. The sample includes reusable components, like the TeamsActivityHandler, for handling Invoke Activity in Teams. It provides a step-by-step setup guide to authenticate users with identity providers such as Microsoft Entra ID, GitHub, and others. |
| 10 | SharePoint Embedded Agent | SharePoint Embedded agent SDK | Advanced | This SDK provides a React component that allows you to add copilot experience to your SharePoint Embedded React application. This agent offers the following features:<ul><li>Reason over documents in Sharepoint Embedded containers and user-accessible content in Sharepoint Online.<li>Developers can configure the application code to limit the search scope to file, folder, and containers.</li><li>Developers can customize and configure chat control including starter prompts, suggested prompts, colors and more.</li></ul> Click [here](https://aka.ms/start-spe-copilot) to learn more.|

## Frequently Asked Questions

### Q: How does the Microsoft 365 Agents SDK work with Copilot Studio? 

Copilot Studio is the recommended path to explore creating agents easily and quickly, using the Power Platform. It provides the capabilities for using and applying Generative AI experiences, manually authored topics and more. The intention is to continue working on making easier ways for agents to work together, regardless of where or how they are built. There are two ways Copilot Studio can be used with the Agents SDK today: 

- Developers can create their main agent experience using the Agents SDK and refer to other agents, including those built using Copilot Studio. There is a sample in the repo to get started above called 'Copilot Studio Skill'
- They can use the Agents SDK to create a ‘Skill’ and implement this from within Copilot Studio. A sample is coming soon for this.

### Q: Where can I add in my own AI Services to build an AI Agent 

By using the Agents SDK, you can additionally bring in other SDKs like the Semantic Kernel. A common requirement is to implement Azure OpenAI Services to create an AI Agent. A sample of that can be found below, which uses Semantic Kernel for orchestration.  

### Q: What is happening to the Microsoft Bot Framework? 

The Agents SDK is the evolution of the Bot Framework v4. The Bot Framework was previously how a developer can-built bots when the primary focus in Conversational AI was around topics, dialogs and messages. The industry has evolved to be driven by a lot of Generative AI functionality, grounding on knowledge that is located all over the enterprise (including outside of it) and orchestrating actions from within a conversational experience. The Agents SDK provides these capabilities for modern day agent development, bringing together the creation of conversational agents and conversation management, orchestration, AI Services and connecting to clients, including the capabilities to bring third party agents into your architecture if required. Developers wanting to use modern SDKs that leverage the latest in the industry should use the Agent Framework and the included SDKs to build their agents. 

### Q: What about the Bot Framework Composer and Emulator? 

The Bot Framework Composer was created to make development on the Bot Framework easier with a UI layer. Using the Agents SDK, there is a modern set of SDKs for developers to build AI Agents using the technology of their choice. Going forward, for those wanting to create agents using an user interface, Copilot Studio is the recommended route to do that, of which agents can be easily integrated to code-first agents or functionally expanded with skills using the Agents SDK.

The Bot Framework Emulator has been updated to support Windows and Linux. It can be used to test locally, in additional to dev tunnels. We are actively working on improving the testing and debugging experience. 

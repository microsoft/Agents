<!--
This section is mastered from the M365 Agents Repo and manually added to the readme of 
other repos. 
-->

# M365 Agents and Copilot Studio Pro-Code ecosystem
Copilot Studio, the M365 Agents SDK, and the surrounding ecosystem spans multiple GitHub repos. This section describes each area and the associated Repos. 

The [Copilot Studio Blog] contains up-to-date information about the latest releases and features across both the low-code and pro-code ecosystem. 


## Samples and Best Practices
Copilot Studio directly supports many code-first development experiences and extensibility points. Samples such as Single Sign On, consumption of REST APIs, and customizing the end-user UX offer a good getting started point. 

### Copilot Studio and M365 Agents samples
| Name    | Repo                             | Notes                    |
|:---------------|:--------------------------------|:--------------------------------|
| Copilot Studio Samples | [Copilot Studio Samples]      | Samples and training material for extending Copilot Studio with code and external services. |
| M365 Agents SDK Samples | [Agents SDK Samples]   | Samples and training material for the M365 Agents SDK. This includes samples around Copilot Studio Skills.|
| WebChat Samples | [WebChat Samples] | Samples for using and customizing WebChat. This includes branding, customization, multi-modal support, and other features. |
| Copilot Studio Snippets | [Copilot Studio Snippets]      | Samples for extending and leveraging Copilot Studio, with a mix of low and pro code customizations. Includes such items as UX customization, citations extensions, SharePoint actions, and other useful examples. |
| Copilot Studio Patterns and Best Practices | [Copilot Studio Guidance] | Microsoft Copilot Studio guidance documentation provides best practices, implementation, and architecture guidance information from the team that works with our enterprise customers. |

### Azure and Azure AI Agent Samples
| Name    | Repo                             | Notes                    |
|:---------------|:--------------------------------|:--------------------------------|
| Azure AI Samples | [Azure AI Samples] | Top-level directory for official Azure AI sample code and examples. It includes notebooks and sample code that contain end-to-end samples as well as smaller code snippets for common developer tasks. |
| Microsoft AI Extension Samples | [Microsoft.Extensions.AI] | Microsoft.Extensions.AI is a set of core .NET libraries developed in collaboration with the .NET ecosystem, including Semantic Kernel. These libraries provide a unified layer of C# abstractions for interacting with AI services, such as small and large language models (SLMs and LLMs) and embeddings. |

## Copilot Studio Documentation
Each area pertaining to Agent development has documentation to help get started. 

| Area    | Description |
|:------------|:--------------------------------|
| [Copilot Studio Getting Started]   | Copilot Studio is a graphical, low-code tool for both creating an agent—including building automation with Power Automate—and extending a Microsoft 365 Copilot with your own enterprise data and scenarios. |
| [Copilot Studio Guidance] | Microsoft Copilot Studio guidance documentation provides best practices, implementation, and architecture guidance information from the team that works with our enterprise customers. |
| [M365 Agents Documentation] | M365 Agents SDK Documentation on GitHub Pages. |
| [M365 Agents Learn Documentation] | M365 Agents SDK Documentation on Learn. |

## Community, Events, and Training
| Area    | Description |
|:------------|:--------------------------------|
| [BotBuilder Community] | A collection of repos led by the community, containing extensions, including middleware, dialogs, recognizers and more for the Microsoft Bot Framework SDK. |
| [Copilot Studio Community] | Microsoft Copilot Studio general forum hosted as part of the Power Platform. |
| [Copilot Studio Events] | Upcoming partner training opportunities to learn more about the power of next-generation AI with Microsoft Copilot solutions.

## M365 Agents SDK
The Microsoft 365 Agents SDK simplifies building full stack, multichannel, trusted agents for platforms including M365, Teams, Copilot Studio, and WebChat. The M365 Agents SDK is code-first solution that enables developers to build and interact with Agents using .NET, JavaScript, or Python. 

The source for the client libraries exists in repositories for each language. This repository is meant to be a jumping off point into those language specific repositories. Issues related to a specific language should be opened in the corresponding repository while cross cutting issues should be opened in the [Agents Repository].

| Language    | Repo                             | Documentation                    |
|:------------|:--------------------------------|:--------------------------------|
| General     |[Agents Repository]               | [M365 Agents Learn Documentation] |
| C# /.NET    |[Agents-for-net Repository]       | [M365 Agents SDK .NET Documentation] |
| JavaScript  |[Agents-for-js Repository]        | [M365 Agents JavaScript Documentation] |
| Python      |[Agents-for-python Repository]    | [M365 Agents Python Documentation]     |

## Agents Tooling
| Repo    | Description |
|:------------|:--------------------------------|
| [Adaptive Cards]   | The versatile UI framework for Teams, Copilot and Outlook integrations. |
| [Bot Framework DirectLineJS]   | Protocol Library for DirectLine, which is used by WebChat. |
| [Teams AI Repository]   | This SDK is specifically designed in creating bots capable of interacting with Teams and Microsoft 365 applications. Works together with the M365 Agents SDK and Teams Toolkit |
| [Agents Toolkit Repository] | Agents Toolkit provides support for building Agents for all major Microsoft 365 platform extensibility surfaces, including Copilot for Microsoft 365, tabs, bots, message extensions for Teams as well as Outlook Add-ins |
| [WebChat]   | Web Chat is a highly customizable web-based client chat control that provides the ability for users to interact with Agent directly in a web page. |

## Legacy Repos
The older Bot Framework repos contain many samples and implementations that may still be of interest. While superseded by the M365 Agents SDK, these repos are mature and interesting for any number of developers. 

| Name    | Repo                             | Notes                    |
|:---------------|:--------------------------------|:--------------------------------|
| Bot Framework SDK                |[Bot Framework SDK]               | [Bot Framework SDK Docs] |
| Bot Framework SDK for .NET       |[Bot Framework SDK for .NET]      | [M365 Agents SDK .NET Documentation] |
| Bot Framework SDK for JavaScript |[Bot Framework SDK for JavaScript]        | [M365 Agents JavaScript Documentation] |
| Bot Framework SDK for Python     |[Bot Framework SDK for Python]    | [M365 Agents Python Documentation]     |
| Bot Framework Emulator | [Bot Framework Emulator] | Electron based application for local debugging of Bots / Agents. Replaced by the M365 Agents Toolkit. 


[Adaptive Cards]: https://adaptivecards.microsoft.com/

[Agents Repository]: https://github.com/Microsoft/Agents
[Agents SDK Samples]: https://github.com/microsoft/Agents/tree/main/samples
[Agents-for-net Repository]: https://github.com/Microsoft/Agents-for-net
[Agents-for-js Repository]: https://github.com/Microsoft/Agents-for-js
[Agents-for-python Repository]: https://github.com/Microsoft/Agents-for-python

[M365 Agents Learn Documentation]: https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/
[M365 Agents SDK .NET Documentation]: https://learn.microsoft.com/en-us/dotnet/api/?view=m365-agents-sdk&preserve-view=true
[M365 Agents JavaScript Documentation]: https://learn.microsoft.com/en-us/javascript/api/overview/agents-overview?view=agents-sdk-js-latest&preserve-view=true
[M365 Agents Python Documentation]: https://learn.microsoft.com/en-us/python/api/agent-sdk-python/agents-overview?view=agent-sdk-python-latest&preserve-view=true

[BotBuilder Community]: https://github.com/BotBuilderCommunity/
[Bot Framework DirectLineJS]: https://github.com/microsoft/BotFramework-DirectLineJS
[Bot Framework SDK]: https://github.com/microsoft/botframework-sdk
[Bot Framework SDK for .NET]: https://github.com/Microsoft/botbuilder-dotnet
[Bot Framework SDK for JavaScript]: https://github.com/Microsoft/botbuilder-js
[Bot Framework SDK for Python]: https://github.com/Microsoft/botbuilder-python

[Bot Framework SDK Docs]: https://learn.microsoft.com/en-us/azure/bot-service/bot-service-overview?view=azure-bot-service-4.0

[Copilot Studio Blog]: https://aka.ms/CopilotStudioBlog
[Copilot Studio Community]: https://aka.ms/CopilotStudioCommunity
[Copilot Studio Events]: https://partner.microsoft.com/en-us/asset/collection/copilot-partner-training-events#
[Copilot Studio Getting Started]: https://aka.ms/CopilotStudioDocs
[Copilot Studio Guidance]: https://aka.ms/CopilotStudioGuidance
[Copilot Studio Samples]: https://github.com/microsoft/CopilotStudioSamples
[Copilot Studio Snippets]: https://github.com/pnp/powerplatform-snippets/tree/main/copilot-studio

[Microsoft.Extensions.AI]: https://github.com/dotnet/ai-samples/tree/main/src/microsoft-extensions-ai

[WebChat]: https://github.com/microsoft/BotFramework-WebChat
[WebChat Samples]: https://github.com/microsoft/BotFramework-WebChat/tree/main/samples
[Bot Framework Emulator]: https://github.com/microsoft/BotFramework-Emulator
[Teams AI Repository]: https://github.com/microsoft/teams-ai
[Agents Toolkit Repository]: https://github.com/OfficeDev/Teams-Toolkit

[Azure AI Samples]: https://github.com/Azure-Samples/azureai-samples

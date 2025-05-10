# Microsoft 365 Agents SDK

The Microsoft 365 Agents SDK simplifies building full stack, multichannel, trusted agents for platforms including M365, Teams, Copilot Studio, and Webchat. We also offer integrations with 3rd parties such as Facebook Messenger, Slack, or Twilio. The SDK provides developers with the building blocks to create agents that handle user interactions, orchestrate requests, reason responses, and collaborate with other agents.

The M365 Agents SDK is a comprehensive framework for building enterprise-grade agents, enabling developers to integrate components from the Azure AI, Semantic Kernel, as well as AI components from other vendors.
 
* **Microsoft 365 Agents SDK Overview**. Provides agent composition and user experience capabilities. Developers can deploy agents to channels for human interaction. This component handles conversation management and enables the agent to access orchestration and core agent backend functionality. In addition, this component enables agents to access functionality from Copilot Studio agents and the Copilot trust layer.
* **Copilot Studio**. Copilot Studio is an end-to-end conversational AI platform that empowers the creation of agents using natural language or a graphical interface. With Copilot Studio, you can easily design, test, and publish agents that suit your needs for internal or external scenarios across your industry, department, or role.
* **Microsoft Teams**. Microsoft Teams offers a collection of apps that are provided by Microsoft or external services. Teams apps can be tabs, bots, or message extensions or any combination of the capabilities. You can [extend Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/overview) apps to work on Outlook and Microsoft 365 App, too. These apps expand the value of the Teams collaborative experience for users.

## Related SDKs

* **Semantic Kernel**. [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
is an SDK that integrates Large Language Models (LLMs) like
[OpenAI](https://platform.openai.com/docs/introduction),
[Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service),
and [Hugging Face](https://huggingface.co/)
with conventional programming languages like C#, Python, and Java. Semantic Kernel achieves this
by allowing you to define [plugins](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins)
that can be chained together
in just a [few lines of code](https://learn.microsoft.com/en-us/semantic-kernel/ai-orchestration/chaining-functions?tabs=Csharp#using-the-runasync-method-to-simplify-your-code).

* **Azure AI Foundry**. Provides the core backend agent functionality, providing reasoning, retrieval augmentation, observability, among other core functions. Developers can leverage the Azure AI Agent Service or build their agent functionality with individual components from this SDK. 

* **SharePoint Embedded Copilot**. The SharePoint Embedded Copilot SDK provides a React component that allows you to add a copilot agent to your SharePoint Embedded React application. You can dynamically scope the knowledge for the copilot interaction to a single file, folder or a set of containers mapped to the application. The SDK also allows you to configure the agent text, starter prompts and copilot instructions. Click [here](https://github.com/microsoft/SharePoint-Embedded-Samples/blob/main/Copilot/README.md) to learn more.

The M365 Agents SDK brings together core components to allow for developers to create, manage and utilize AI Services and third-party SDKs to power orchestration and develop custom agent logic functionality.

## Links

The source for the client libraries exists for the most part in repositories for each language. This repository is meant to be a jumping off point into those language specific repositories. Issues related to a specific language should be opened in the corresponding repository but cross cutting issues can be opened in this repository.

| Language    | Repo                             | Documentation                    |
|:------------|:--------------------------------:|:--------------------------------:|
| General     |[Agents Repository]               | [Agents SDK Documentation](https://aka.ms/M365-Agents-SDK-Docs)   |
| C# /.NET    |[Agents-for-net Repository]       | Coming Soon             |
| JavaScript  |[Agents-for-js Repository]        | Coming Soon       |
| Python      |[Agents-for-python Repository]    | Coming Soon           |

## Evolution of Bot Framework

The Microsoft 365 Agents SDK is the evolution of the Bot Framework. The Bot Framework was previously how a developer can build bots where the primary focus in Conversational AI was around topics, dialogs and messages. The industry has evolved to be driven by Generative AI functionality, grounding on knowledge that is located across the enterprise, and there is the need for companies to be able to orchastrate actions, in additional to answer questions, from within a conversational experience. The Microsoft 365 Agents SDK provides capabilities for modern agent development, bringing together the creation of conversational agents with conversation management, orchestration, connecting to numerous AI Services and clients, including agents created with third party software or technology.

Please be aware as we move through this evolution, there will be some references to Bot Framework within documentation and in the code. 



## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


## Recommended VS Code Extensions

To ensure a consistent development environment, we recommend using the following VS Code extensions:

|Extension  |Description  |
|---------|---------|
|[ESLint](https://marketplace.visualstudio.com/items?itemName=dbaeumer.vscode-eslint)|Integrates [ESLint](https://eslint.org/) into VS Code. Allows you to find an fix problems in JavaScript code.|
|[GitHub Copilot Chat](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot-chat)|[GitHub Copilot](https://code.visualstudio.com/docs/copilot/overview) is an AI peer programming tool that helps you write code faster and smarter.|
|[GitHub Codespaces](https://marketplace.visualstudio.com/items?itemName=GitHub.codespaces)|[GitHub Codespaces](https://github.com/features/codespaces) provides cloud-hosted development environments for any activity - whether it's a long-term project, or a short-term task like reviewing a pull request.|
|[C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)|C# Dev Kit helps you manage your code with a solution explorer and test your code with integrated unit test discovery and execution, elevating your C# development experience wherever you like to develop (Windows, macOS, Linux, and even in a Codespace).|
|[Adaptive Card Previewer](https://marketplace.visualstudio.com/items?itemName=TeamsDevApp.vscode-adaptive-cards)|The Microsoft Adaptive Card Previewer uses the latest rendering stack for Microsoft Teams to provide a more accurate preview of Adaptive Cards. You can open a side-by-side preview to view changes live, toggle between light, dark, and high-contrast themes.|

These are listed in `.vscode/extensions.json`, so VS Code should prompt you to install them when you open the project.


## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

[Agents Repository]: https://github.com/Microsoft/Agents
[Agents-for-net Repository]: https://github.com/Microsoft/Agents-for-net
[Agents-for-js Repository]: https://github.com/Microsoft/Agents-for-js
[Agents-for-python Repository]: https://github.com/Microsoft/Agents-for-python

[Official Agents Documentation]: https://aka.ms/AgentsFramework
[.NET Documentation]: https://aka.ms/Agents-net-docs
[JavaScript Documentation]: https://aka.ms/agents-js-docs
[Python Documentation]: https://aka.ms/agents-python-docs

# Register a Single Tenant Agent using Client Secrets for an Azure Bot

This article shows how to register a agent with Azure AI Bot Service.

Your can manage your agent identity in Azure in a few different ways.

- As a user-assigned managed identity, so that you don't need to manage the bot's credentials yourself.
- As a single-tenant app using secrets, certificates, or Federated Credentials.

> These instructions are for SingleTenant Identity with a Client Secret.
> If you are on the Microsoft Tenant, using either MultiTenant or SingleTenant with a client secret is prohibited.  There are limited options for running locally in this case. Certiciate SN+I is a viable alternative. If this doesn't work for you, the only alternative is to use [Managed Identity](azurebot-create-msi.md) and deploy the agent code to Azure and run there.

## Create the resource

Create the Azure Bot resource. This allows you to register your agent with the Azure AI Bot Service.

1. Go to the Azure portal.

1. In the right pane, select **Create a resource**.

1. Find and select the **Azure Bot** card.

   ![Azure Bot Resource](media/azure-bot-resource.png)

1. Select **Create**.

1. Enter values in the required fields and review and update settings.

   a. Provide information under Project details. Select whether your agent will have global or local data residency. Currently, the local data residency feature is available for resources in the "westeurope" and "centralindia" region. For more information, see [Regionalization in Azure AI Bot Service](https://learn.microsoft.com/en-us/azure/bot-service/bot-builder-concept-regionalization?view=azure-bot-service-4.0).

   ![Azure Bot Settings](media/azure-bot-project-details.png)

   b. Provide information under Microsoft App ID. Select how your agent identity will be managed in Azure and whether to create a new identity or use an existing one.

   ![Azure Bot Identity](media/azure-bot-ms-app-id-single.png)

1. Select **Review + create**.

1. If the validation passes, select **Create**.

1. Once the deployment is completed, select **Go to resource**. You should see the agent and related resources listed in the resource group you selected.

1. If this is a Teams bot
   1. Select **Settings** on the left sidebar, then **Channels**.
   1. Select **Microsoft Teams** from the list and choose appropriate options.

1. Select **Settings**, then **Configuration**

1. Select **Manage Password** next to **Microsoft App ID**
   ![Azure Bot Configuration](media/azure-bot-configuration-single.png)

1. On the **Overview** pane, record the **Application (client) ID** and **Directory (tenant) ID**

1. Select **Certificates & secrets** on the left then **Client secrets**

1. Create a new secret by click **New client secret**

> [!IMPORTANT]
> Very important Copy the new secret and store in a safe place.  You need the information later when configuring your agent code.

## Further reading
- [Configuring DotNet Agent Auth](https://aka.ms/AgentsSDK-DotNetMSALAuth)
- [Configuring JS Agent Auth](https://aka.ms/AgentsSDK-JSMSALAuth)
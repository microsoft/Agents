# Provision an Azure Bot using User Assigned Managed Identity

This article shows how to register an agent with Azure AI Bot Service using User Assigned Managed Identity.

> User Assigned Managed Identity will not work for local debugging via devtunnels.

## Create the resource

Create the Azure Bot resource. This allows you to register your agent with the Azure AI Bot Service.

1. Go to the Azure portal.

1. In the right pane, select **Create a resource**.

1. Find and select the **Azure Bot** card.

   ![Azure Bot Resource](media/azure-bot-resource.png)

1. Select **Create**.

1. Enter values in the required fields and review and update settings.

   a. Provide information under **Project details**. Select whether your agent will have global or local data residency. Currently, the local data residency feature is available for resources in the "westeurope" and "centralindia" region. For more information, see [Regionalization in Azure AI Bot Service](https://learn.microsoft.com/en-us/azure/bot-service/bot-builder-concept-regionalization?view=azure-bot-service-4.0).

   ![Azure Bot Settings](media/azure-bot-project-details.png)

   b. Provide information under **Microsoft App ID**. Select how your agent identity will be managed in Azure and whether to create a new identity or use an existing one.

   ![Azure Bot Identity](media/azure-bot-ms-app-id.png)

1. Select **Review + create**.

1. If the validation passes, select **Create**.

1. Once the deployment is completed, select **Go to resource**. You should see the agent and related resources listed in the resource group you selected.

1. If this is a Teams bot

   1. Select **Settings** on the left sidebar, then **Channels**.
   1. Select **Microsoft Teams** from the list and choose appropriate options.

> [!IMPORTANT]
> Store the **ClientID** of the User Managed Identity.  You need the information later when configuring your agent configuration.

## Next Steps
- [Configuring DotNet Agent Auth](./MSALAuthConfigurationOptions.md#usermanagedidentity)
- [Configuring JS Agent Auth](./azurebot-auth-for-js.md#single-tenant---user-assigned-managed-identity)

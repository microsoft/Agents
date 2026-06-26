# Deployment Guide — Production Reference Sample

This guide covers deploying the production-reference support triage agent to Azure using the Bicep template in `infra/main.bicep`.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) version 2.50 or later
- Bicep CLI: `az bicep install`
- An active Azure subscription: `az account show`
- An existing **Azure Bot Service registration** with a client ID and client secret. See [Create a bot resource](https://learn.microsoft.com/azure/bot-service/abs-quickstart) if you need to create one.
- The bot client secret must be available to pass as a deployment parameter. Do **not** hard-code it in source files.

## Deployment overview

The Bicep template provisions:

| Resource | Purpose |
|----------|---------|
| App Service plan (B1, Linux) | Hosting plan for the .NET 8 web app |
| App Service (system-assigned managed identity) | Runs the agent; health check path `/health/live`; HTTPS only |
| Storage account + blob container (`agents-state`) | Durable conversation state |
| Key Vault | Stores `BotClientSecret`; RBAC authorization enabled |
| Log Analytics workspace | Telemetry backend |
| Application Insights | Connected to Log Analytics; connection string written to app settings |
| Role assignment: Storage Blob Data Contributor | Grants the web app managed identity read/write access to the blob container |
| Role assignment: Key Vault Secrets User | Grants the web app managed identity read access to Key Vault secrets |
| App settings | Wires together storage URI, token validation, connection settings, and Application Insights |

Bot Service registration is **not** created by Bicep. Configure the messaging endpoint manually after deploy (see step 5 below).

## Step 1: Log in to Azure

```bash
az login
az account set --subscription "<your-subscription-id>"
```

## Step 2: Create a resource group

```bash
az group create --name "<rg-name>" --location "<azure-region>"
# Example: az group create --name prod-ref-rg --location eastus
```

## Step 3: Build the application package

```bash
dotnet publish samples/dotnet/production-reference/ProductionReference.csproj \
  --configuration Release \
  --output ./publish-output
```

```powershell
# PowerShell equivalent
dotnet publish samples\dotnet\production-reference\ProductionReference.csproj `
  --configuration Release `
  --output .\publish-output
```

Compress the output for deployment (App Service `WEBSITE_RUN_FROM_PACKAGE`):

```bash
cd publish-output && zip -r ../deploy.zip . && cd ..
```

```powershell
Compress-Archive -Path .\publish-output\* -DestinationPath .\deploy.zip -Force
```

## Step 4: Deploy the Bicep template

### Providing `botClientSecret` securely

The `botClientSecret` parameter is declared `@secure()` in the Bicep template. Azure Resource Manager does not log secure parameter values.

**Option A — interactive prompt (development or one-off deploy):**

```bash
az deployment group create \
  --resource-group "<rg-name>" \
  --template-file samples/dotnet/production-reference/infra/main.bicep \
  --parameters \
      namePrefix="<prefix>" \
      botClientId="<your-bot-app-id>" \
      botClientSecret="<your-bot-client-secret>" \
      tenantId="<your-tenant-id>"
```

**Option B — parameters file with a Key Vault reference (recommended for automation):**

Copy and edit `infra/main.parameters.json`, substituting your subscription ID, resource group, and Key Vault name. Then run:

```bash
az deployment group create \
  --resource-group "<rg-name>" \
  --template-file samples/dotnet/production-reference/infra/main.bicep \
  --parameters @samples/dotnet/production-reference/infra/main.parameters.json
```

The parameters file uses `"reference": { "keyVault": {...}, "secretName": "..." }` to pull `botClientSecret` from an existing Key Vault at deploy time without the value ever appearing in shell history or CI logs.

## Step 5: Capture deployment outputs

```bash
az deployment group show \
  --resource-group "<rg-name>" \
  --name "<deployment-name>" \
  --query properties.outputs
```

| Output | Description |
|--------|-------------|
| `appServiceDefaultHostName` | Hostname of the deployed App Service (e.g. `mybot-app-xxxx.azurewebsites.net`) |
| `messagingEndpoint` | Full messaging endpoint URL to register in Azure Bot Service |
| `storageContainerUri` | URI of the blob container used for agent state |
| `keyVaultName` | Name of the provisioned Key Vault |

## Step 6: Configure the Bot Service messaging endpoint

In the [Azure Portal](https://portal.azure.com), navigate to your existing Azure Bot resource:

1. Open **Configuration**.
2. Set **Messaging endpoint** to the `messagingEndpoint` output value (e.g. `https://mybot-app-xxxx.azurewebsites.net/api/messages`).
3. Save.

## Step 7: Deploy the application package

```bash
az webapp deploy \
  --resource-group "<rg-name>" \
  --name "<web-app-name>" \
  --src-path deploy.zip \
  --type zip
```

## Managed identity role assignments

Both role assignments are provisioned by the Bicep `webAppSettings` resource, which `dependsOn` the role assignments. This sequencing ensures that the managed identity has the required access before the App Service attempts to resolve the Key Vault reference at startup.

| Role | Scope | Why |
|------|-------|-----|
| `Storage Blob Data Contributor` | Blob container | Allows `BlobsStorage` (via `DefaultAzureCredential`) to read and write state blobs |
| `Key Vault Secrets User` | Key Vault | Allows App Service Key Vault reference resolution for `BotClientSecret` at app startup |

If role propagation is slow, the first app startup may fail to resolve the Key Vault reference. Wait 30–60 seconds and restart the app, or check the RUNBOOK for remediation steps.

## App Service settings and Key Vault reference behavior

The Bicep template writes the following App Service application settings:

| Setting | Value |
|---------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `WEBSITE_RUN_FROM_PACKAGE` | `1` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | From Application Insights resource |
| `AZURE_BLOB_STORAGE_CONTAINER_URI` | URI of the provisioned blob container |
| `TokenValidation__Enabled` | `true` |
| `TokenValidation__Audiences__0` | Bot client ID |
| `TokenValidation__TenantId` | Entra tenant ID |
| `Connections__ServiceConnection__Settings__AuthType` | `ClientSecret` |
| `Connections__ServiceConnection__Settings__AuthorityEndpoint` | Entra authority URL |
| `Connections__ServiceConnection__Settings__ClientId` | Bot client ID |
| `Connections__ServiceConnection__Settings__ClientSecret` | `@Microsoft.KeyVault(SecretUri=...)` — resolved by App Service |
| `ConnectionsMap__0__ServiceUrl` | `*` |
| `ConnectionsMap__0__Connection` | `ServiceConnection` |

The `ClientSecret` setting uses an [App Service Key Vault reference](https://learn.microsoft.com/azure/app-service/app-reference-keyvault). App Service resolves the reference at startup using the web app managed identity. The actual secret value is never stored in App Service configuration.

## Post-deploy validation

```bash
# Liveness probe — should return 200
curl https://<appServiceDefaultHostName>/health/live

# Readiness probe — should return 200 with {"status":"Healthy"}
curl https://<appServiceDefaultHostName>/health/ready

# Validate Bicep template syntax locally
az bicep build --file samples/dotnet/production-reference/infra/main.bicep

# Run xUnit tests
dotnet test samples/dotnet/production-reference/tests/ProductionReference.Tests.csproj
```

Send a test message through the Bot Service channel (Teams or Agents Playground) to confirm end-to-end message delivery and state persistence.

## Teardown

```bash
az group delete --name "<rg-name>" --yes --no-wait
```

> **Note:** Deleting the resource group also deletes the Key Vault. If soft delete is enabled (as provisioned), the Key Vault is soft-deleted and retains its name reservation for `softDeleteRetentionInDays` (7 days as provisioned). To purge immediately: `az keyvault purge --name "<kv-name>"`.

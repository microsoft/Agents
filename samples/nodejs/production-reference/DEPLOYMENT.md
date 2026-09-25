# Deploy to Azure App Service

## Prerequisites

- Azure CLI and Bicep.
- Permission to create Azure Bot, Web Chat, managed identity, and role-assignment resources.

## Provision

Resource names use the [Microsoft Cloud Adoption Framework abbreviations](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations): `app`, `bot`, `appi`, `id`, `log`, `asp`, and `st`. Resources use `<app-name>-<abbreviation>`. Storage accounts do not allow hyphens, so Storage uses `<app-name-without-hyphens>st`.

List the available subscriptions:

```bash
az account list --output table
```

Select the target subscription if the Azure CLI is using a different subscription:

```bash
az account set --subscription <subscription-id-or-name>
```

If this command or the deployment returns `AADSTS9002313`, sign in with an account in the tenant that owns the subscription, then select the subscription again:

```bash
az logout
az login --tenant <tenant-id>
az account set --subscription <subscription-id-or-name>
```

Create the resource group first if it does not exist:

```bash
az group create --name <resource-group> --location <azure-region>
```

Deploy the sample infrastructure to the resource group. The template creates an App Service, Azure Bot, Web Chat channel, user-assigned agent identity, Storage account, Blob data role assignment, Log Analytics workspace, and Application Insights.

App Service, Azure Bot, and Storage names must be globally unique. If Azure reports a name conflict, choose a more specific `<app-name>`, such as `<workload>-<environment>-<owner>`.

PowerShell:

```powershell
az deployment group create --resource-group <resource-group> --template-file infra/main.bicep --parameters appName=<unique-app-name> location=<azure-region>
```

Bash:

```bash
az deployment group create --resource-group <resource-group> --template-file infra/main.bicep --parameters appName=<unique-app-name> location=<azure-region>
```

The template configures Azure Bot to use the same user-assigned managed identity as App Service, sets its messaging endpoint to `https://<app-host>/api/messages`, and enables Web Chat. Use Bot SKU `S1` instead of the default `F0` when the production workload requires it: `botSkuName=S1`.

## App Service settings

The Bicep template configures these settings. Keep them in deployment automation, not a committed environment file:

```text
NODE_ENV=production
SCM_DO_BUILD_DURING_DEPLOYMENT=true
NPM_CONFIG_INCLUDE=dev
BLOB_CONTAINER_URL=https://<storage-account>.blob.core.windows.net/agents-production-reference-state
connections__serviceConnection__settings__clientId=<agent-managed-identity-client-id>
connections__serviceConnection__settings__tenantId=<tenant-id>
connections__serviceConnection__settings__authType=UserManagedIdentity
connections__serviceConnection__settings__validateIssuer=true
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
connectionsMap__0__audience=<agent-managed-identity-client-id>
OutboundHostValidator__Enabled=true
OutboundHostValidator__IncludeDefaultMicrosoftHosts=false
OutboundHostValidator__Hosts=webchat.botframework.com
APPLICATIONINSIGHTS_CONNECTION_STRING=<application-insights-connection-string>
OTEL_SERVICE_NAME=agents-sdk-production-reference
```

The SDK uses `*` only to select its default named connection. JWT audience and issuer validation run first. HTTP middleware then checks that the activity `serviceUrl` host is `webchat.botframework.com`. The configured outbound-host policy restricts token-bearing SDK client paths and validates the service URL against the authenticated claim.

This bounded deployment deliberately disables the built-in Microsoft host list and explicitly allows only `webchat.botframework.com`. If you add another channel or token-bearing SDK client, identify, configure, and test every required host before deployment.

Use a Key Vault reference only if a required dependency cannot authenticate with managed identity. Do not add a Blob client secret.

## Deploy the application code

The Bicep deployment creates and configures the App Service. It does not upload the application code. From the sample directory, create a ZIP whose root contains the source and package files.

PowerShell:

```powershell
Compress-Archive -Path src,package.json,package-lock.json,tsconfig.json -DestinationPath app.zip -Force
```

Bash:

```bash
zip -r app.zip src package.json package-lock.json tsconfig.json
```

Deploy the ZIP. App Service uses `SCM_DO_BUILD_DURING_DEPLOYMENT=true` to install dependencies and run `npm run build`. `NPM_CONFIG_INCLUDE=dev` makes the remote build install TypeScript and the type packages that it needs. `NODE_ENV=production` still configures the running application for production.

```bash
az webapp deploy --resource-group <resource-group> --name <app-name>-app --src-path app.zip --type zip --clean true --restart true --timeout 600000
```

The timeout is 10 minutes and applies to the Azure CLI wait operation. If the command returns a gateway timeout but Oryx continues to build, wait for the deployment to finish before you retry or run the smoke test.

## Deploy and smoke test

1. Run `npm ci && npm test` from this sample.
2. Create and deploy `app.zip` as shown above.
3. Set `APP_URL=https://<app-name>-app.azurewebsites.net` and run `npm run test:smoke`. It verifies liveness and readiness.
4. Use the Azure Bot resource's Web Chat test or a token-based Web Chat client, then send an issue summary and impact. Never expose the Web Chat secret in browser code.
5. Restart the App Service. In the same conversation, verify state remains available.
6. Check telemetry for request failures and Blob dependency latency. Verify no issue summary, raw activity, user identifier, token, or credential is present.
7. Verify `/api/messages` rejects missing, invalid, wrong-audience, and wrong-issuer JWTs; a signed activity with a non-Web-Chat service URL; and a signed activity whose service URL differs from its token claim.

## Rollback

Deploy the last known-good application package. Do not delete the state account during rollback. If state schema changes, deploy backward-compatible readers before writers and retain a rollback-compatible version until old conversations expire.

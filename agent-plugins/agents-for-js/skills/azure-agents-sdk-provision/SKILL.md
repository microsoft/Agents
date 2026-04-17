---
name: azure-agents-sdk-provision
description: Use when provisioning Azure resources for a Microsoft Agents SDK application - creating an Azure Bot resource, configuring identity credentials, adding Teams channel, or setting up OAuth connections via az CLI
---

# Azure Agents SDK Provisioning

## Overview

Provisions Azure Bot resources for M365 Agents SDK apps using `az` CLI commands. Three auth types available; each produces a config block for `appsettings.json` (dotnet) or env vars (Node.js).

**See `agents-sdk-typescript` skill for env var format (Node.js).**

## Prerequisites

```bash
az login
az account set --subscription "<subscription-id>"
# Create resource group if needed
az group create --name "<rg>" --location eastus
```

## Auth Type Selection

| Auth Type | No Secret | Works Off-Azure | App Registration | JS SDK |
|-----------|:---------:|:---------------:|:----------------:|:------:|
| `UserManagedIdentity` | ✅ | ❌ | ❌ | ✅ |
| `FederatedCredentials` | ✅ | ✅ | ✅ | ✅ |
| `ClientSecret` | ❌ | ✅ | ✅ | ✅ |

### UserManagedIdentity

The bot authenticates as an Azure Managed Identity — a system-managed credential that Azure rotates automatically. No app registration, no secrets, no expiry management.

**Use when:** The bot is hosted on Azure (App Service, Container Apps, AKS, Azure Functions). The hosting platform injects the identity token; it cannot work outside Azure.

**Implies:**
- Azure assigns a `clientId` for the identity; no secret is ever stored or transmitted
- The identity is scoped to the resource group where it's created
- Simplest operational model — nothing to rotate, nothing to leak
- If the host is compromised, the attacker can only act as that identity (blast radius limited to assigned roles)

**Not suitable for:** Local dev (no Azure runtime to inject tokens), cross-tenant scenarios, off-Azure CI/CD pipelines.

---

### FederatedCredentials

The bot has an App Registration (for a stable `clientId` and tenant-scoped identity), but instead of a secret, it uses a Managed Identity to prove ownership via a federated credential. The MSI's `principalId` is registered as a trusted subject on the app — Azure AD accepts the MSI's token as proof that the app is authorized.

**Use when:** You need an App Registration (e.g. for OAuth scopes, Graph API access, cross-tenant identity) but don't want to manage a client secret. Common for production workloads still hosted on Azure.

**Implies:**
- Still requires Azure hosting (MSI token still injected by the platform)
- Two Azure resources: a Managed Identity + an App Registration linked by the federated credential
- No secret ever exists — the FIC relationship is the credential
- Slightly more setup complexity than pure MSI, but unlocks app-registration capabilities (API permissions, OAuth connections, service principal)
- If the MSI or app registration is deleted, the trust breaks — both must be managed together

**Not suitable for:** Local dev, off-Azure deployments.

---

### ClientSecret

The bot has an App Registration with a generated client secret. The secret is stored in config and sent to Azure AD to obtain tokens. Classic service principal authentication.

**Use when:** The bot runs outside Azure (local dev, on-prem, other cloud), the JS SDK is in use, or you need the quickest path to a working bot without MSI infrastructure.

**Implies:**
- A secret exists and must be protected — store in Key Vault, GitHub Secrets, or environment secret manager; **never in source control**
- Secrets expire (default 1–2 years) and must be rotated before expiry, or the bot stops authenticating
- Widest attack surface: a leaked secret allows anyone to authenticate as the bot from anywhere
- Easiest to use in CI/CD and local dev (just set env vars)
- `az ad app credential reset` generates a new secret and immediately invalidates the old one — coordinate rotation carefully

**Not suitable for:** High-security production environments where secret management overhead is unacceptable.

---

## Step 1: Create Identity & Credentials

### Option A: UserManagedIdentity

```bash
RESULT=$(az identity create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --output json)

CLIENT_ID=$(echo $RESULT | jq -r '.clientId')
TENANT_ID=$(echo $RESULT | jq -r '.tenantId')
RESOURCE_ID=$(echo $RESULT | jq -r '.id')
```

Config output:
```json
{
  "ClientId": "<clientId>",
  "TenantId": "<tenantId>",
  "ResourceId": "<id>",
  "AzureBotAppType": "UserAssignedMSI",
  "ServiceConnection.Settings": {
    "AuthType": "UserManagedIdentity",
    "ClientId": "<clientId>",
    "Scopes": ["https://api.botframework.com/.default"]
  }
}
```

### Option B: FederatedCredentials

```bash
# 1. Create managed identity
MSI=$(az identity create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" --output json)
MSI_CLIENT_ID=$(echo $MSI | jq -r '.clientId')
MSI_PRINCIPAL_ID=$(echo $MSI | jq -r '.principalId')
TENANT_ID=$(echo $MSI | jq -r '.tenantId')

# 2. Create app registration
APP=$(az ad app create \
  --display-name "$BOT_NAME" \
  --sign-in-audience "AzureADMyOrg" --output json)
APP_ID=$(echo $APP | jq -r '.appId')

# 3. Create federated credential (subject = MSI principalId, NOT clientId)
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters "{
    \"name\": \"agent\",
    \"description\": \"Agent-to-Channel\",
    \"issuer\": \"https://login.microsoftonline.com/${TENANT_ID}/v2.0\",
    \"subject\": \"${MSI_PRINCIPAL_ID}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

# 4. Create service principal
az ad sp create --id "$APP_ID" --output none
```

Config output:
```json
{
  "ClientId": "<appId>",
  "TenantId": "<tenantId>",
  "AzureBotAppType": "SingleTenant",
  "ServiceConnection.Settings": {
    "AuthType": "FederatedCredentials",
    "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
    "ClientId": "<appId>",
    "FederatedClientId": "<msi-clientId>",
    "Scopes": ["https://api.botframework.com/.default"]
  }
}
```

### Option C: ClientSecret

```bash
# 1. Create app registration
APP=$(az ad app create \
  --display-name "$BOT_NAME" \
  --sign-in-audience "AzureADMyOrg" --output json)
APP_ID=$(echo $APP | jq -r '.appId')

# 2. Generate client secret (save the password — it won't be shown again)
SECRET=$(az ad app credential reset \
  --id "$APP_ID" --only-show-errors --output json)
CLIENT_SECRET=$(echo $SECRET | jq -r '.password')

# 3. Get tenant ID
TENANT_ID=$(az account list \
  --query "[?isDefault].tenantId | [0]" \
  --only-show-errors --output tsv)

# 4. Create service principal
az ad sp create --id "$APP_ID" --output none
```

Config output (store secret in Key Vault or environment secret store — never in source):
```json
{
  "ClientId": "<appId>",
  "TenantId": "<tenantId>",
  "AzureBotAppType": "SingleTenant",
  "ServiceConnection.Settings": {
    "AuthType": "ClientSecret",
    "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
    "ClientId": "<appId>",
    "ClientSecret": "<secret>",
    "Scopes": ["https://api.botframework.com/.default"]
  }
}
```

---

## Step 2: Create Azure Bot Resource

> **Do NOT use `az bot create`** — the `az bot` command group hardcodes API version `2021-05-01-preview`, which Azure has retired. This fails even on the latest Azure CLI. Use `az rest` directly instead.

```bash
SUBSCRIPTION=$(az account show --query id --output tsv)

# UserAssignedMSI
az rest --method PUT \
  --uri "https://management.azure.com/subscriptions/${SUBSCRIPTION}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.BotService/botServices/${BOT_NAME}?api-version=2022-09-15" \
  --body "{\"location\":\"global\",\"sku\":{\"name\":\"F0\"},\"kind\":\"azurebot\",\"properties\":{\"displayName\":\"${BOT_NAME}\",\"msaAppId\":\"${CLIENT_ID}\",\"msaAppType\":\"UserAssignedMSI\",\"msaAppMSIResourceId\":\"${RESOURCE_ID}\",\"msaAppTenantId\":\"${TENANT_ID}\",\"endpoint\":\"\"}}"

# SingleTenant (FederatedCredentials or ClientSecret)
az rest --method PUT \
  --uri "https://management.azure.com/subscriptions/${SUBSCRIPTION}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.BotService/botServices/${BOT_NAME}?api-version=2022-09-15" \
  --body "{\"location\":\"global\",\"sku\":{\"name\":\"F0\"},\"kind\":\"azurebot\",\"properties\":{\"displayName\":\"${BOT_NAME}\",\"msaAppId\":\"${APP_ID}\",\"msaAppType\":\"SingleTenant\",\"msaAppTenantId\":\"${TENANT_ID}\",\"endpoint\":\"\"}}"
```

### Add Teams Channel (optional)

```bash
az bot teams create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME"
```

---

## Step 3: Apply Config

**dotnet (`appsettings.json`):**
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "...",
        "ClientId": "...",
        "...": "..."
      }
    }
  }
}
```

**Node.js (`.env`):**

Use `connections__<name>__settings__<field>` (double underscore separators). Always add a `connectionsMap` entry.

*ClientSecret:*
```
connections__serviceConnection__settings__clientId=<appId>
connections__serviceConnection__settings__clientSecret=<secret>
connections__serviceConnection__settings__tenantId=<tenantId>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

*UserManagedIdentity* (hosted on Azure — no secret needed):
```
connections__serviceConnection__settings__clientId=<msi-clientId>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

*FederatedCredentials:*
```
connections__serviceConnection__settings__clientId=<appId>
connections__serviceConnection__settings__FICClientId=<msi-clientId>
connections__serviceConnection__settings__tenantId=<tenantId>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

> Run with: `node --env-file .env dist/index.js` (Node 20+)

---

## OAuth Connection Setup (Post-Creation)

Adds a user sign-in OAuth connection to an existing bot.

### ClientSecret bots (AadV2)

For bots using ClientSecret auth, use the `Aadv2` service provider. You can reuse the bot's existing app registration or create a separate OAuth app.

```bash
az bot authsetting create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --setting-name "$OAUTH_CONNECTION_NAME" \
  --client-id "$APP_ID" \
  --client-secret "$CLIENT_SECRET" \
  --provider-scope-string "https://graph.microsoft.com/User.Read openid profile" \
  --service "Aadv2" \
  --parameters tenantId="$TENANT_ID"
```

**Required: Add redirect URI to the app registration** — without this, users will get `AADSTS500113: No reply address is registered`:

```bash
az ad app update \
  --id "$APP_ID" \
  --web-redirect-uris "https://token.botframework.com/.auth/web/redirect"
```

**Node.js env var:**
```
graph_connectionName=GraphOAuthConnection
```

### FIC bots (AadV2WithFic)

Auth type `AadV2WithFic` only. Requires the bot to have a managed identity already configured.

```bash
# 1. Get bot's MSI principal ID
MSI_PRINCIPAL_ID=$(az identity show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --query "principalId" --output tsv)
MSI_CLIENT_ID=$(az identity show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --query "clientId" --output tsv)

# 2. Create OAuth app registration (reuse existing or create new)
OAUTH_APP=$(az ad app create \
  --display-name "${BOT_NAME}-oauth" \
  --sign-in-audience "AzureADMyOrg" --output json)
OAUTH_APP_ID=$(echo $OAUTH_APP | jq -r '.appId')
TENANT_ID=$(az account list --query "[?isDefault].tenantId | [0]" --output tsv)

# 3. Create federated credential on OAuth app
az ad app federated-credential create \
  --id "$OAUTH_APP_ID" \
  --parameters "{
    \"name\": \"agent-oauth\",
    \"description\": \"OAuth Agent-to-Channel\",
    \"issuer\": \"https://login.microsoftonline.com/${TENANT_ID}/v2.0\",
    \"subject\": \"${MSI_PRINCIPAL_ID}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

# 4. Register OAuth connection on bot
az bot authsetting create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --setting-name "$OAUTH_CONNECTION_NAME" \
  --client-id "$OAUTH_APP_ID" \
  --client-secret "" \
  --provider-scope-string "api://${OAUTH_APP_ID}/user_impersonation" \
  --service "Aadv2WithFic" \
  --parameters \
    tenantId="$TENANT_ID" \
    tokenExchangeUrl="api://${OAUTH_APP_ID}" \
    federatedClientId="$MSI_CLIENT_ID"
```

### Teams SSO (optional)

After step 2, expose `access_as_user` scope on the OAuth app and add Teams client app IDs as authorized client applications via the Azure Portal (Entra ID > App Registrations > Expose an API).

### Exchangeable Token (optional)

Add `--parameters exchangeableToken="true"` to the `az bot authsetting create` command.

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Using `az bot create` | Broken — hardcodes retired API version `2021-05-01-preview`. Use `az rest --method PUT` with `api-version=2022-09-15` instead (see Step 2) |
| FIC subject uses `clientId` | Use `principalId` (object ID) from `az identity create` |
| Skipped `az ad sp create` | Always create service principal after `az ad app create` |
| Wrong `app-type` | `UserAssignedMSI` for MSI bots; `SingleTenant` for app-reg bots |
| Client secret committed to source | Use Key Vault, env secrets, or GitHub Secrets |
| OAuth app not found | Run `az ad sp create --id <oauth-appId>` if bot can't find the app |
| `AADSTS500113: No reply address is registered` | Add `https://token.botframework.com/.auth/web/redirect` as a redirect URI on the app registration |

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

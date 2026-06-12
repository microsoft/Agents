---
name: azure-agents-sdk-provision-dotnet
description: >
  Use when provisioning Azure resources for a Microsoft Agents SDK application
  in C# / .NET — including creating an Azure Bot Service resource, setting up
  Entra app registrations, configuring identity credentials
  (UserManagedIdentity, FederatedCredentials, or ClientSecret), adding a Teams
  channel, running Bicep deployments for SSO, or setting up OAuth user sign-in
  connections via az CLI. Trigger even if the user doesn't say "provision" — use
  on requests like "set up my bot on Azure", "deploy my bot", "configure Azure
  for my bot", or "get Teams SSO working."
---

# Azure Agents SDK Provisioning (.NET)

## Overview

Provisions Azure Bot resources for M365 Agents SDK .NET apps using `az` CLI commands. Three auth types available; each produces a config block for `appsettings.json`.

**See `agents-sdk-dotnet` skill for full `appsettings.json` format and patterns.**

## Prerequisites

```bash
az login
az account set --subscription "<subscription-id>"
# Create resource group if needed
az group create --name "<rg>" --location eastus
```

## Auth Type Selection

| Auth Type | No Secret | Works Off-Azure | App Registration |
|-----------|:---------:|:---------------:|:----------------:|
| `UserManagedIdentity` | Yes | No | No |
| `FederatedCredentials` | Yes | Yes | Yes |
| `ClientSecret` | No | Yes | Yes |

### UserManagedIdentity

The bot authenticates as an Azure Managed Identity — a system-managed credential that Azure rotates automatically. No app registration, no secrets, no expiry management.

**Use when:** The bot is hosted on Azure (App Service, Container Apps, AKS, Azure Functions).

**Implies:**
- Azure assigns a `ClientId` for the identity; no secret is ever stored or transmitted
- Simplest operational model — nothing to rotate, nothing to leak
- Cannot work outside Azure (no runtime to inject tokens)

---

### FederatedCredentials

The bot has an App Registration, but instead of a secret, it uses a Managed Identity to prove ownership via a federated credential.

**Use when:** You need an App Registration (for OAuth scopes, Graph API access, cross-tenant identity) but don't want to manage a client secret. Common for production workloads on Azure.

**Implies:**
- Still requires Azure hosting
- Two Azure resources: a Managed Identity + an App Registration linked by the federated credential
- No secret ever exists

---

### ClientSecret

The bot has an App Registration with a generated client secret.

**Use when:** The bot runs outside Azure (local dev, on-prem, other cloud), or you need the quickest path to a working bot.
**How to execute:** Use Option C in Step 1 below, which runs two Bicep deployments with Teams SSO support, then generates a client secret.

**Implies:**
- A secret exists and must be protected — store in Key Vault or Azure App Configuration; **never in source control**
- Secrets expire (default 1–2 years) and must be rotated

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

**appsettings.json output:**
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "UserManagedIdentity",
        "ClientId": "<clientId>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    { "ServiceUrl": "*", "Connection": "ServiceConnection" }
  ],
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<clientId>"],
    "TenantId": "<tenantId>"
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

**appsettings.json output:**
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "FederatedCredentials",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<appId>",
        "FederatedClientId": "<msi-clientId>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    { "ServiceUrl": "*", "Connection": "ServiceConnection" }
  ],
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<appId>"],
    "TenantId": "<tenantId>"
  }
}
```

### Option C: ClientSecret

Uses two Bicep deployments to create a Teams SSO-capable app registration, then generates a client secret.

**Prerequisites:** Bicep CLI 0.26.0+ (`az bicep install`) with the Microsoft Graph Bicep extension. Account requires Application Administrator or Global Administrator role.

**Step 0 — Verify active tenant matches the intended tenant:**

```bash
az account show --query "{tenantId:tenantId, tenantDomain:tenantDefaultDomain, subscription:name}" --output table
```

If the active tenant does not match, switch first:

```bash
az login --tenant <intended-tenant-domain-or-id>
az account set --subscription "<subscription-id>"
```

**Ask the user for:**
- `APP_NAME` — display name for the Entra app registration (must be unique in the tenant)
- `RESOURCE_GROUP` — run `az group list --query "[].{Name:name, Location:location}" --output table` then ask the user to pick one

**Step 1 — Check if app already exists (handle re-run after partial failure):**

```bash
EXISTING_APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
if [ -n "$EXISTING_APP_ID" ]; then
  echo "App '$APP_NAME' already exists (appId: $EXISTING_APP_ID) — resuming."
  APP_ID="$EXISTING_APP_ID"
  OAUTH_SCOPE_ID=$(az ad app show --id "$APP_ID" \
    --query "api.oauth2PermissionScopes[?value=='access_as_user'].id | [0]" -o tsv)
  if [ -z "$OAUTH_SCOPE_ID" ]; then
    echo "ERROR: Existing app has no 'access_as_user' scope. Delete the app and re-run."
    exit 1
  fi
  echo "Re-using OAUTH_SCOPE_ID: $OAUTH_SCOPE_ID — skipping Phase 1, proceeding to Phase 2."
fi
```

**Phase 1 — Create app registration with `access_as_user` scope and identifier URI:**

> **Skip this phase if APP_ID and OAUTH_SCOPE_ID are already set** from the resume block above.

The Bicep template files are located in the JS plugin's assets folder and are shared across both SDKs:
- `agent-plugins/agents-for-js/skills/azure-agents-sdk-provision/assets/Create_SSO_AppRegistration.bicep`
- `agent-plugins/agents-for-js/skills/azure-agents-sdk-provision/assets/Create_SSO_PreAuthorize.bicep`

```bash
OWNER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

# Generate a new GUID for the OAuth scope (PowerShell on Windows)
OAUTH_SCOPE_ID=$(powershell -NoProfile -Command "[guid]::NewGuid().ToString()")

RESULT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "Create_SSO_AppRegistration.bicep" \
  --parameters "APP_NAME=$APP_NAME" "OWNER_OBJECT_ID=$OWNER_OBJECT_ID" "OAUTH_SCOPE_ID=$OAUTH_SCOPE_ID" \
  --output json)

APP_ID=$(echo $RESULT | jq -r '.properties.outputs.newAppId.value')
```

**Phase 2 — Pre-authorize Teams/Office host clients for SSO:**

```bash
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "Create_SSO_PreAuthorize.bicep" \
  --parameters "APP_NAME=$APP_NAME" "OAUTH_SCOPE_ID=$OAUTH_SCOPE_ID"
```

**Phase 2b — Verify deployment:**

```bash
echo "=== Verifying app registration ==="

IDENTIFIER_URI=$(az ad app show --id "$APP_ID" --query "identifierUris[0]" -o tsv)
[ "$IDENTIFIER_URI" = "api://botid-$APP_ID" ] \
  && echo "PASS  Identifier URI: $IDENTIFIER_URI" \
  || echo "FAIL  Identifier URI — expected 'api://botid-$APP_ID', got '$IDENTIFIER_URI'"

SCOPE_CHECK=$(az ad app show --id "$APP_ID" \
  --query "api.oauth2PermissionScopes[?value=='access_as_user'].id | [0]" -o tsv)
[ -n "$SCOPE_CHECK" ] \
  && echo "PASS  access_as_user scope present (id: $SCOPE_CHECK)" \
  || echo "FAIL  access_as_user scope missing"

PRE_AUTH_COUNT=$(az ad app show --id "$APP_ID" \
  --query "length(api.preAuthorizedApplications)" -o tsv 2>/dev/null || echo 0)
[ "${PRE_AUTH_COUNT:-0}" -ge 9 ] \
  && echo "PASS  Pre-authorized clients: $PRE_AUTH_COUNT" \
  || echo "FAIL  Pre-authorized clients: ${PRE_AUTH_COUNT:-0} (expected 9 — re-run Phase 2)"
```

If any check fails, do not proceed.

**Phase 3 — Register service principal and create client secret:**

```bash
az ad sp create --id "$APP_ID" --output none

SECRET_RESULT=$(az ad app credential reset \
  --id "$APP_ID" \
  --append \
  --output json)

CLIENT_SECRET=$(echo $SECRET_RESULT | jq -r '.password')
TENANT_ID=$(echo $SECRET_RESULT | jq -r '.tenant')
SECRET_EXPIRY=$(az ad app credential list --id "$APP_ID" --query "[0].endDateTime" -o tsv)
```

Record these values — `CLIENT_SECRET` is **not retrievable again**:
- `APP_ID` — App ID (Client ID)
- `CLIENT_SECRET` — client secret
- `TENANT_ID` — tenant ID
- `SECRET_EXPIRY` — secret expiry date (rotate before this date)

Always surface the expiry date prominently in the output.

**appsettings.json output:**

Store secret in Key Vault or environment secret store — never in source.

```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthType": "ClientSecret",
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenantId>",
        "ClientId": "<appId>",
        "ClientSecret": "<secret>",
        "Scopes": ["https://api.botframework.com/.default"]
      }
    }
  },
  "ConnectionsMap": [
    { "ServiceUrl": "*", "Connection": "ServiceConnection" }
  ],
  "TokenValidation": {
    "Enabled": true,
    "Audiences": ["<appId>"],
    "TenantId": "<tenantId>"
  }
}
```

**Teams app manifest snippet (`manifest.json`):**

```json
"webApplicationInfo": {
  "id": "<appId>",
  "resource": "api://botid-<appId>"
}
```

**Secret rotation (for existing bots):**

Do **not** use `az ad app credential reset` without `--append` on a running bot — it invalidates all existing secrets.

```bash
# Step 1 — Add NEW secret (--append keeps old secret live)
NEW_SECRET_RESULT=$(az ad app credential reset --id "$APP_ID" --append --output json)
NEW_SECRET=$(echo $NEW_SECRET_RESULT | jq -r '.password')
NEW_KEY_ID=$(az ad app credential list --id "$APP_ID" \
  --query "sort_by(@, &endDateTime)[-1].keyId" -o tsv)

# Step 2 — Deploy new secret to appsettings/Key Vault, verify the bot is healthy

# Step 3 — Remove old secret
az ad app credential list --id "$APP_ID" --query "[].{keyId:keyId, expiry:endDateTime}" -o table
OLD_KEY_ID=<keyId of the old secret>
az ad app credential delete --id "$APP_ID" --key-id "$OLD_KEY_ID"
```

---

## Step 2: Create Azure Bot Resource

> **Do NOT use `az bot create`** — the `az bot` command group hardcodes a retired API version. Use `az rest` directly.

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

Place the `appsettings.json` output from Step 1 into your project root. For production, use Azure Key Vault references or user secrets:

```bash
# Use .NET user secrets for local dev
dotnet user-secrets init
dotnet user-secrets set "Connections:ServiceConnection:Settings:ClientSecret" "<secret>"
```

Or reference Key Vault in `appsettings.json`:
```json
{
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "ClientSecret": "@Microsoft.KeyVault(VaultName=myVault;SecretName=BotClientSecret)"
      }
    }
  }
}
```

---

## OAuth Connection Setup (Post-Creation)

If the user needs to add a user sign-in OAuth connection to the bot, the setup procedure is identical to the JS version. The key steps are:

### ClientSecret bots (AadV2)

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

**Required: Add redirect URI:**
```bash
az ad app update \
  --id "$APP_ID" \
  --web-redirect-uris "https://token.botframework.com/.auth/web/redirect"
```

**appsettings.json — add to AgentApplication section:**
```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "graph",
      "AutoSignin": true,
      "Handlers": {
        "graph": {
          "Settings": {
            "AzureBotOAuthConnectionName": "GraphOAuthConnection",
            "Title": "Sign In",
            "Text": "Please sign in to continue"
          }
        }
      }
    }
  }
}
```

### FIC bots (AadV2WithFic)

Same `az` CLI commands as the JS version — see the JS provision skill's `references/oauth-setup.md` for the full FIC OAuth setup procedure. The only difference is the config output format:

**appsettings.json — same `AgentApplication:UserAuthorization` block as above.**

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Using `az bot create` | Broken — hardcodes retired API. Use `az rest --method PUT` with `api-version=2022-09-15` |
| Wrong tenant active in `az` session | Run `az account show` before starting — commands silently succeed in the wrong tenant |
| Duplicate app name | Run `az ad app list --display-name "$APP_NAME"` before deploying |
| FIC subject uses `clientId` | Use `principalId` (object ID) from `az identity create` |
| Skipped `az ad sp create` | Always create service principal after `az ad app create` |
| Wrong `app-type` | `UserAssignedMSI` for MSI bots; `SingleTenant` for app-reg bots |
| Client secret in source control | Use Key Vault, user-secrets, or Azure App Configuration |
| Secret expiry not tracked | Always surface the expiry date — default is ~1 year |
| `credential reset` without `--append` on a running bot | Causes downtime. Always use `--append`, deploy new secret, then delete old key |
| Re-running Phase 1 with new GUID after partial failure | Re-derive `OAUTH_SCOPE_ID` from the existing app instead |
| `AADSTS500113: No reply address is registered` | Add `https://token.botframework.com/.auth/web/redirect` as a redirect URI |
| `uuidgen: command not found` on Windows | Use `powershell -NoProfile -Command "[guid]::NewGuid().ToString()"` |

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

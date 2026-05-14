---
name: azure-agents-sdk-provision
description: >
  Use when provisioning Azure resources for a Microsoft Agents SDK application —
  including creating an Azure Bot Service resource, setting up Entra app
  registrations, configuring identity credentials (UserManagedIdentity,
  FederatedCredentials, or ClientSecret), adding a Teams channel, running Bicep
  deployments for SSO, or setting up OAuth user sign-in connections via az CLI.
  Trigger even if the user doesn't say "provision" — use on requests like "set
  up my bot on Azure", "deploy my bot", "configure Azure for my bot", or "get
  Teams SSO working."
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
**How to execute:**  Use Option C in Step 1 below, which runs two Bicep deployments to create the app registration with Teams SSO support, then generates a client secret.

**Implies:**
- A secret exists and must be protected — store in Key Vault, GitHub Secrets, or environment secret manager; **never in source control**
- Secrets expire (default 1–2 years) and must be rotated before expiry, or the bot stops authenticating
- Widest attack surface: a leaked secret allows anyone to authenticate as the bot from anywhere
- Easiest to use in CI/CD and local dev (just set env vars)
- `az ad app credential reset --append` adds a new secret without invalidating existing ones — always use `--append`, then remove the old secret key ID after deploying

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

Uses two Bicep deployments to create a Teams SSO-capable app registration, then generates a client secret.

**Prerequisites:** Bicep CLI 0.26.0+ (`az bicep install`) with the Microsoft Graph Bicep extension. Account requires Application Administrator or Global Administrator role.

**Step 0 — Verify active tenant matches the intended tenant:**

Run this before collecting any other inputs. If the user specified a tenant domain (e.g. `asdkt3.onmicrosoft.com`), confirm it matches before proceeding.

```bash
az account show --query "{tenantId:tenantId, tenantDomain:tenantDefaultDomain, subscription:name}" --output table
```

If the active tenant does not match the intended tenant, switch first:

```bash
az login --tenant <intended-tenant-domain-or-id>
az account set --subscription "<subscription-id>"
```

**Ask the user for:**
- `APP_NAME` — display name for the Entra app registration (must be unique in the tenant)
- `RESOURCE_GROUP` — run the command below to show available resource groups, then ask the user to pick one:

```bash
az group list --query "[].{Name:name, Location:location}" --output table
```

- Config format — ask: **"Which config format do you need — dotnet (`appsettings.json`) or Node.js (`.env`)?"**

**Step 1 — Check if app already exists (handle re-run after partial failure):**

```bash
EXISTING_APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
if [ -n "$EXISTING_APP_ID" ]; then
  echo "App '$APP_NAME' already exists (appId: $EXISTING_APP_ID) — resuming."
  APP_ID="$EXISTING_APP_ID"
  # Re-derive scope ID from the existing app rather than generating a new GUID
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

> **Skip this phase if APP_ID and OAUTH_SCOPE_ID are already set** from the resume block above — the app registration already exists.

```bash
# Acquire owner object ID from the signed-in user
OWNER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

# Generate a new GUID for the OAuth scope (use PowerShell on Windows)
OAUTH_SCOPE_ID=$(powershell -NoProfile -Command "[guid]::NewGuid().ToString()")

# Use the file at [assets/Create_SSO_AppRegistration.bicep](assets/Create_SSO_AppRegistration.bicep)
RESULT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "assets/Create_SSO_AppRegistration.bicep" \
  --parameters "APP_NAME=$APP_NAME" "OWNER_OBJECT_ID=$OWNER_OBJECT_ID" "OAUTH_SCOPE_ID=$OAUTH_SCOPE_ID" \
  --output json)

APP_ID=$(echo $RESULT | jq -r '.properties.outputs.newAppId.value')
```

**Phase 2 — Pre-authorize Teams/Office host clients for SSO:**

```bash
# Use the file at [assets/Create_SSO_PreAuthorize.bicep](assets/Create_SSO_PreAuthorize.bicep)
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "assets/Create_SSO_PreAuthorize.bicep" \
  --parameters "APP_NAME=$APP_NAME" "OAUTH_SCOPE_ID=$OAUTH_SCOPE_ID"
```

**Phase 2b — Verify deployment:**

Run after Phase 2 completes. All three checks must pass before generating a secret.

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

If any check fails, do not proceed. Re-run the failed phase before continuing.

**Phase 3 — Register service principal and create client secret:**

```bash
# Ignore "already in use" error if the service principal already exists
az ad sp create --id "$APP_ID" --output none

# --append adds the new secret alongside any existing ones (safe for running bots)
SECRET_RESULT=$(az ad app credential reset \
  --id "$APP_ID" \
  --append \
  --output json)

CLIENT_SECRET=$(echo $SECRET_RESULT | jq -r '.password')
TENANT_ID=$(echo $SECRET_RESULT | jq -r '.tenant')

# Retrieve the expiry date of the generated secret
SECRET_EXPIRY=$(az ad app credential list --id "$APP_ID" --query "[0].endDateTime" -o tsv)
```

Record these values — `CLIENT_SECRET` is **not retrievable again**:
- `APP_ID` — App ID (Client ID)
- `CLIENT_SECRET` — client secret
- `TENANT_ID` — tenant ID
- `SECRET_EXPIRY` — secret expiry date (rotate before this date or the bot stops authenticating)

Always surface the expiry date prominently in the output to the user.

**Config output — dotnet (`appsettings.json`):**

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
  }
}
```

**Config output — Node.js (`.env`):**

```
connections__serviceConnection__settings__clientId=<appId>
connections__serviceConnection__settings__clientSecret=<secret>
connections__serviceConnection__settings__tenantId=<tenantId>
connectionsMap__0__connection=serviceConnection
connectionsMap__0__serviceUrl=*
```

> Run with: `node --env-file .env dist/index.js` (Node 20+)

**Teams app manifest snippet (`manifest.json`):**

Add this block to enable SSO in Teams. The `resource` value must match the identifier URI set on the app registration.

```json
"webApplicationInfo": {
  "id": "<appId>",
  "resource": "api://botid-<appId>"
}
```

**Secret rotation (for existing bots):**

Do **not** use `az ad app credential reset` without `--append` on a running bot — it immediately invalidates all existing secrets and causes downtime. The safe rotation pattern is:

```bash
# Step 1 — Add a NEW secret alongside the existing one (--append keeps old secret live)
NEW_SECRET_RESULT=$(az ad app credential reset \
  --id "$APP_ID" \
  --append \
  --output json)
# Record the new secret and its key ID
NEW_SECRET=$(echo $NEW_SECRET_RESULT | jq -r '.password')
NEW_KEY_ID=$(az ad app credential list --id "$APP_ID" \
  --query "sort_by(@, &endDateTime)[-1].keyId" -o tsv)

# Step 2 — Deploy the new secret to config/Key Vault, verify the bot is healthy

# Step 3 — Remove the OLD secret by its key ID (list first to find it)
az ad app credential list --id "$APP_ID" --query "[].{keyId:keyId, expiry:endDateTime}" -o table
OLD_KEY_ID=<keyId of the old secret from the table above>
az ad app credential delete --id "$APP_ID" --key-id "$OLD_KEY_ID"
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

If the user needs to add a user sign-in OAuth connection to the bot, read [references/oauth-setup.md](references/oauth-setup.md) for the full setup procedure (ClientSecret/AadV2, FIC/AadV2WithFic, Teams SSO pre-authorization, API permissions).

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Using `az bot create` | Broken — hardcodes retired API version `2021-05-01-preview`. Use `az rest --method PUT` with `api-version=2022-09-15` instead (see Step 2) |
| Wrong tenant active in `az` session | Run `az account show` before starting and verify `tenantDefaultDomain` matches the intended tenant — commands silently succeed in the wrong tenant |
| Duplicate app name | Run `az ad app list --display-name "$APP_NAME"` before deploying — duplicate names cause confusing Bicep errors |
| FIC subject uses `clientId` | Use `principalId` (object ID) from `az identity create` |
| Skipped `az ad sp create` | Always create service principal after `az ad app create` |
| Wrong `app-type` | `UserAssignedMSI` for MSI bots; `SingleTenant` for app-reg bots |
| Client secret committed to source | Use Key Vault, env secrets, or GitHub Secrets |
| Secret expiry not tracked | Always retrieve and surface the expiry date with `az ad app credential list --id "$APP_ID" --query "[0].endDateTime"` — default is ~1 year |
| `credential reset` without `--append` on a running bot | Immediately invalidates all existing secrets — causes downtime. Always use `--append`, deploy the new secret, then delete the old key ID |
| Re-running Phase 1 with a new GUID after a partial failure | Generates a second `access_as_user` scope on the existing app. Instead, re-derive `OAUTH_SCOPE_ID` from the existing app (see Step 1 resume block) |
| OAuth app not found | Run `az ad sp create --id <oauth-appId>` if bot can't find the app |
| `AADSTS500113: No reply address is registered` | Add `https://token.botframework.com/.auth/web/redirect` as a redirect URI on the app registration — applies to **both** ClientSecret and FIC OAuth app registrations |
| Teams SSO token exchange fails silently | The `access_as_user` scope must be created on the OAuth app registration (step 4 of FIC flow) **before** pre-authorizing Teams clients; and `--provider-scope-string` must reference `access_as_user`, not `user_impersonation` |
| `uuidgen: command not found` on Windows Git Bash | Use `python3 -c "import uuid; print(uuid.uuid4())"` instead of `uuidgen` |

## Contributing

If you hit a problem this skill couldn't solve, found a workaround, or noticed something wrong or outdated, that's valuable — please help improve this skill for everyone.

Draft a suggested issue title and body based on the conversation, then ask the user to open it at: https://github.com/microsoft/agents/issues/new

A good issue includes:
- What the user was trying to do
- What went wrong (errors, unexpected behavior)
- What worked — including any workaround found during this conversation
- Relevant code or config snippets

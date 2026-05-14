# OAuth Connection Setup

Adds a user sign-in OAuth connection to an existing bot.

## ClientSecret bots (AadV2)

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

## FIC bots (AadV2WithFic)

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

# 4. Set application ID URI and create the access_as_user scope
az ad app update \
  --id "$OAUTH_APP_ID" \
  --identifier-uris "api://botid-$OAUTH_APP_ID"

OAUTH_OBJECT_ID=$(az ad app show --id "$OAUTH_APP_ID" --query id --output tsv)
# uuidgen on Linux/macOS/WSL; python3 fallback for Windows Git Bash
SCOPE_ID=$(uuidgen 2>/dev/null || python3 -c "import uuid; print(uuid.uuid4())")

az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/$OAUTH_OBJECT_ID" \
  --headers "Content-Type=application/json" \
  --body "{
    \"api\": {
      \"oauth2PermissionScopes\": [{
        \"id\": \"$SCOPE_ID\",
        \"adminConsentDescription\": \"Allow the app to access the bot on behalf of the signed-in user.\",
        \"adminConsentDisplayName\": \"Access the bot as a user\",
        \"isEnabled\": true,
        \"type\": \"User\",
        \"userConsentDescription\": \"Allow this app to access the bot on your behalf.\",
        \"userConsentDisplayName\": \"Access the bot as a user\",
        \"value\": \"access_as_user\"
      }]
    }
  }"

# 5. Register OAuth connection on bot
az bot authsetting create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$BOT_NAME" \
  --setting-name "$OAUTH_CONNECTION_NAME" \
  --client-id "$OAUTH_APP_ID" \
  --client-secret "" \
  --provider-scope-string "api://${OAUTH_APP_ID}/access_as_user" \
  --service "Aadv2WithFic" \
  --parameters \
    tenantId="$TENANT_ID" \
    tokenExchangeUrl="api://${OAUTH_APP_ID}" \
    federatedClientId="$MSI_CLIENT_ID"

# 6. Add redirect URI to OAuth app registration
az ad app update \
  --id "$OAUTH_APP_ID" \
  --web-redirect-uris "https://token.botframework.com/.auth/web/redirect"
```

## Teams SSO (optional)

Pre-authorize the Teams client apps to allow silent token acquisition. The `access_as_user` scope was created in step 4 above and `SCOPE_ID`/`OAUTH_OBJECT_ID` must still be set in the same shell session.

```bash
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/$OAUTH_OBJECT_ID" \
  --headers "Content-Type=application/json" \
  --body "{
    \"api\": {
      \"preAuthorizedApplications\": [
        { \"appId\": \"1fec8e78-bce4-4aaf-ab1b-5451cc387264\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"5e3ce6c0-2b1f-4285-8d4b-75ee78787346\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"d3590ed6-52b3-4102-aeff-aad2292ab01c\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"bc59ab01-8403-45c6-8796-ac3ef710b3e3\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"0ec893e0-5785-4de6-99da-4ed124e5296c\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"4765445b-32c6-49b0-83e6-1d93765276ca\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"27922004-5251-4030-b22d-91ecd9a37ea4\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"00000002-0000-0ff1-ce00-000000000000\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] },
        { \"appId\": \"4345a7b9-9a63-4910-a426-35363201d503\", \"delegatedPermissionIds\": [\"$SCOPE_ID\"] }
      ]
    }
  }"
```

These are the Microsoft host client app IDs that must be pre-authorized for SSO to work across all Teams and Office surfaces:

| App ID | Client |
|--------|--------|
| `1fec8e78-bce4-4aaf-ab1b-5451cc387264` | Teams desktop / mobile |
| `5e3ce6c0-2b1f-4285-8d4b-75ee78787346` | Teams web |
| `d3590ed6-52b3-4102-aeff-aad2292ab01c` | Microsoft Office desktop |
| `bc59ab01-8403-45c6-8796-ac3ef710b3e3` | Teams iOS |
| `0ec893e0-5785-4de6-99da-4ed124e5296c` | Office Mobile (iOS) |
| `4765445b-32c6-49b0-83e6-1d93765276ca` | Microsoft Teams (secondary client) |
| `27922004-5251-4030-b22d-91ecd9a37ea4` | Skype for Business Online |
| `00000002-0000-0ff1-ce00-000000000000` | Microsoft Office (Outlook Web / legacy) |
| `4345a7b9-9a63-4910-a426-35363201d503` | Office Online |

> **If starting a new session**, re-derive the variables before running the above:
> ```bash
> OAUTH_OBJECT_ID=$(az ad app show --id "$OAUTH_APP_ID" --query id --output tsv)
> SCOPE_ID=$(az ad app show --id "$OAUTH_APP_ID" \
>   --query "api.oauth2PermissionScopes[?value=='access_as_user'].id | [0]" --output tsv)
> ```

## API Permissions (optional)

Add delegated Microsoft Graph permissions when the bot calls the Graph API on behalf of the signed-in user:

```bash
# Add delegated permissions (00000003-... is the well-known Graph API app ID)
az ad app permission add \
  --id "$OAUTH_APP_ID" \
  --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions \
    e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope \
    37f7f235-527c-4136-accd-4a02d197296e=Scope \
    14dad69e-099b-42c9-810b-d002981feec1=Scope

# Grant admin consent (requires Global Admin or Privileged Role Admin)
az ad app permission admin-consent --id "$OAUTH_APP_ID"
```

Common delegated Graph permission IDs:

| Permission | GUID |
|---|---|
| `User.Read` | `e1fe6dd8-ba31-4d61-89e7-88639da4683d` |
| `openid` | `37f7f235-527c-4136-accd-4a02d197296e` |
| `profile` | `14dad69e-099b-42c9-810b-d002981feec1` |
| `email` | `64a6cdd6-aab1-4aad-a773-0ae5ec9b9f9b` |
| `offline_access` | `7427e0e9-2fba-42fe-b0c0-848c9e6a8182` |

## Exchangeable Token (optional)

Add `--parameters exchangeableToken="true"` to the `az bot authsetting create` command.

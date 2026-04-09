// Deploys Azure resources for the LOCAL test environment.
//
// What this provisions:
//   - App Registration + Service Principal  (for agent auth credentials)
//   - Azure Bot Service registration        (registers the bot with Bot Framework)
//   - Key Vault                             (stores the client secret after provisioning)
//
// What it does NOT provision (agents run as local processes):
//   - App Service / ACI  — use environments/cloud/ for hosted agents
//   - User-Assigned Managed Identity  — not usable on a local dev machine
//
// Usage (standalone):
//   az deployment group create -g <rg> -f environments/local/infra/main.bicep \
//     --parameters environments/local/infra/main.bicepparam \
//     --parameters deployerPrincipalId=$(az ad signed-in-user show --query id -o tsv)
//
// Usage (azd — run from environments/local/):
//   azd provision

@minLength(1)
@maxLength(32)
@description('Short environment name used as a suffix on all resource names.')
param environmentName string

@minLength(1)
@description('Primary Azure region for Key Vault.')
param location string = resourceGroup().location

@description('Messaging endpoint for the Azure Bot registration. Use http://localhost:3978/api/messages for anonymous local testing, or a devtunnel URL for JWT scenarios.')
param botEndpoint string = 'https://localhost:3978/api/messages'

@description('Principal ID of the user or service principal running this deployment. Grants Key Vault Secrets Officer so the provision script can write the client secret.')
param deployerPrincipalId string = ''

var tags = { environment: environmentName }
var suffix = toLower(uniqueString(resourceGroup().id, environmentName))
var botName = 'bot-${environmentName}'
var kvName = 'kv-${take(suffix, 20)}'

module appReg './modules/app-registration.bicep' = {
  params: {
    botName: botName
  }
}

module bot './modules/azure-bot.bicep' = {
  params: {
    botName: botName
    appId: appReg.outputs.appId
    tenantId: appReg.outputs.tenantId
    endpoint: botEndpoint
  }
}

module kv './modules/key-vault.bicep' = {
  params: {
    name: kvName
    location: location
    deployerPrincipalId: deployerPrincipalId
    tags: tags
  }
}

// Written to .azure/<env>/.env by azd, or captured via --query properties.outputs.
// inject_config.py reads infra-outputs.json (written by provision.ps1) to populate agent configs.
output APP_ID string = appReg.outputs.appId
output TENANT_ID string = appReg.outputs.tenantId
output KEY_VAULT_NAME string = kv.outputs.name
output KEY_VAULT_URI string = kv.outputs.uri
output BOT_NAME string = botName

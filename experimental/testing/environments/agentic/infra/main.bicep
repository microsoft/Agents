// Deploys Azure resources for the AGENTIC test environment.
//
// What this provisions:
//   - Agent App Registration + Service Principal  (for agent auth credentials)
//   - Azure Bot Service registration              (registers the bot with Bot Framework)
//   - Key Vault                                   (stores the agent client secret)
//   - A365 CLI App Registration                   (custom public-client app for the A365 CLI tool)
//
// What it does NOT provision:
//   - App Service / ACI  — agents run as local subprocesses
//   - User-Assigned Managed Identity  — not usable on a local dev machine
//
// After provisioning, postprovision.ps1 calls pre_setup_a365.ps1 to finalize the
// A365 CLI app registration with settings that cannot be expressed in Bicep.
//
// Usage (azd — run from environments/agentic/):
//   azd provision

@minLength(1)
@maxLength(32)
@description('Short environment name used as a suffix on all resource names.')
param environmentName string

@minLength(1)
@description('Primary Azure region for Key Vault.')
param location string = resourceGroup().location

@description('Messaging endpoint for the Azure Bot registration.')
param botEndpoint string = 'https://localhost:3978/api/messages'

@description('Principal ID of the user or service principal running this deployment. Grants Key Vault Secrets Officer so the provision script can write the client secret.')
param deployerPrincipalId string = ''

var tags = { environment: environmentName }
var suffix = toLower(uniqueString(resourceGroup().id, environmentName))
var botName = 'bot-${environmentName}'
var kvName = 'kv-${take(suffix, 20)}'

module agentAppReg './modules/app-registration.bicep' = {
  params: {
    botName: botName
  }
}

module bot './modules/azure-bot.bicep' = {
  params: {
    botName: botName
    appId: agentAppReg.outputs.appId
    tenantId: agentAppReg.outputs.tenantId
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

module a365AppReg './modules/a365-app-registration.bicep' = {
  params: {
    environmentName: environmentName
  }
}

output APP_ID string = agentAppReg.outputs.appId
output TENANT_ID string = agentAppReg.outputs.tenantId
output KEY_VAULT_NAME string = kv.outputs.name
output KEY_VAULT_URI string = kv.outputs.uri
output BOT_NAME string = botName
output A365_APP_ID string = a365AppReg.outputs.appId

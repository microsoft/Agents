@description('Name for the Azure Bot resource.')
param botName string

@description('App Registration client ID (msaAppId).')
param appId string

@description('Messaging endpoint for the bot. For local testing this is the localhost URL; update to a devtunnel URL for JWT scenarios.')
param endpoint string

@description('Tenant ID for Single-tenant bot registration.')
param tenantId string

@description('Azure region for the Bot Service resource.')
param location string = 'global'

resource azureBot 'Microsoft.BotService/botServices@2023-09-15-preview' = {
  name: botName
  location: location
  kind: 'azurebot'
  sku: {
    name: 'F0'
  }
  properties: {
    displayName: botName
    msaAppId: appId
    // endpoint: endpoint
    msaAppType: 'SingleTenant'
    msaAppTenantId: tenantId
  }
}

output botId string = azureBot.id
output botName string = azureBot.name

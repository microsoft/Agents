@description('The name of the Azure Bot resource.')
param botName string

@description('The ID for an existing App Registration')
param appId string

@description('The endpoint for the bot service.')
param endpoint string

@description('The location for the bot service.')
param location string

resource azureBot 'microsoft.botService/botServices@2023-09-15-preview' = {
  name: botName
  location: location
  kind: 'azurebot'
  properties: {
    displayName: botName
    msaAppId: appId
    endpoint: endpoint
    msaAppType: 'SingleTenant'
    msaAppTenantId: tenant().tenantId
    // schemaTransformationVersion: '1.3'
  }
}

resource msteams 'microsoft.botService/botServices/channels@2023-09-15-preview' = {
  parent: azureBot
  location: location
  name: 'MsTeamsChannel'
  properties: {
    channelName: 'MsTeamsChannel'
  }
}

output appId string = azureBot.id

param botName string

@description('The ID for an existing App Registration')
param appId string

@description('The ID for an existing App Registration configured for OAuth')
param oauthAppId string

@description('The endpoint for the bot service.')
param endpoint string

@description('The location for the bot service.')
param location string

@allowed([
  'aadv2'
])
@description('The OAuth method to use for connections.')
param oauthType string = 'aadv2'

@description('The name for the OAuth connection.')
param oauthConnectionName string

module azureBot '../../bicep/bot.bicep' = {
  name: botName
  params: {
    appId: appId
    botName: botName
    endpoint: endpoint
    location: location
  }
}

// create an OAuth Connection to another App if the application registration IDs differ
resource oauthConnection 'microsoft.botService/botServices/connections@2023-09-15-preview' = if (oauthAppId != appId) {
  dependsOn: [azureBot]
  location: location
  name: '${botName}/${oauthConnectionName}'
  properties: {
    name: oauthConnectionName
    serviceProviderDisplayName:'Azure Active Directory v2'
    serviceProviderId: '30dd229c-58e3-4a48-bdfd-91ec48eb906c'
    clientId: oauthAppId
    clientSecret: '' // needs to be manually set
    scopes: 'api://botid-${oauthAppId}/defaultScope'
    parameters: [
      {
        key: 'tenantId'
        value: tenant().tenantId
      }
      {
        key: 'tokenExchangeUrl'
        value: 'api://botid-${oauthAppId}' 
      }
    ]
  }
}

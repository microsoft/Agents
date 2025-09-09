param botName string

@description('The ID for an existing App Registration')
param appId string

@description('The endpoint for the bot service.')
param endpoint string

@description('The location for the bot service.')
param location string

module azureBot '../../bicep/bot.bicep' = {
  name: botName
  params: {
    appId: appId
    botName: botName
    endpoint: endpoint
    location: location
  }
}

resource graphConnectionSettingsAadv2 'microsoft.botService/botServices/connections@2023-09-15-preview' = {
  dependsOn: [azureBot]
  location: location
  name: '${botName}/graph-oauth'
  properties: {
    name: 'graph-oauth'
    serviceProviderDisplayName: 'Azure Active Directory v2'
    serviceProviderId: '30dd229c-58e3-4a48-bdfd-91ec48eb906c'
    clientId: appId
    clientSecret: '' // needs to be manually set
    scopes: 'User.Read openId profile'
    parameters: [
      {
        key: 'tenantId'
        value: tenant().tenantId
      }
    ]
  }
}

resource githubConnectionSettingsAadv2 'microsoft.botService/botServices/connections@2023-09-15-preview' = {
  dependsOn: [azureBot]
  location: location
  name: '${botName}/github-oauth'
  properties: {
    name: 'github-oauth'
    serviceProviderDisplayName: 'GitHub'
    serviceProviderId: 'd05eaacf-1593-4603-9c6c-d4d8fffa46cb'
    clientId: '' // manually set
    scopes: 'user repo'
  }
}

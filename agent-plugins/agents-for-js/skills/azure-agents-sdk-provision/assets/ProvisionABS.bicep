param BOT_NAME string = 'MattBBot01'
param BOT_DEPLOYMENT_TYPE string = 'SingleTenant'
param APP_ID string = '982f1994-71b1-4cea-be76-77e0649f7b16'
param TENANT_ID string = '367c5af9-6300-4248-99bc-72288021c775'
param DEPLOYMENT_AZURE_REGION string = 'global'

resource botService 'Microsoft.BotService/botServices@2023-09-15-preview' = {
  name: BOT_NAME
  location: DEPLOYMENT_AZURE_REGION
  sku: {
    name: 'S1'
  }
  kind: 'azurebot'
  properties: {
    endpoint: ''
    displayName: BOT_NAME
    msaAppType: BOT_DEPLOYMENT_TYPE
    msaAppId: APP_ID
    msaAppTenantId: TENANT_ID
    isStreamingSupported: false
    schemaTransformationVersion: '1.3'
    tenantId: TENANT_ID
  }
}

resource symbolicname 'Microsoft.BotService/botServices/channels@2023-09-15-preview' = {
  parent: botService
  name: 'MsTeamsChannel'
  location: DEPLOYMENT_AZURE_REGION
  properties: {
    channelName: 'MsTeamsChannel'
    properties: {
      acceptedTerms: true
      isEnabled: true
    }
  }
}

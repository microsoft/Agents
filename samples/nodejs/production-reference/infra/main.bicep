@description('Globally unique App Service and storage name prefix.')
param appName string

param location string = resourceGroup().location
param skuName string = 'B1'

@allowed([
  'F0'
  'S1'
])
param botSkuName string = 'F0'

var storageName = toLower(replace('${appName}st', '-', ''))
var webAppName = '${appName}-app'
var planName = '${appName}-asp'
var appInsightsName = '${appName}-appi'
var workspaceName = '${appName}-log'
var identityName = '${appName}-id'
var botName = '${appName}-bot'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource stateContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'agents-production-reference-state'
  properties: { publicAccess: 'None' }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    features: { enableLogAccessUsingOnlyResourcePermissions: true }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource agentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource plan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: planName
  location: location
  sku: { name: skuName, tier: 'Basic' }
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${agentIdentity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|24-lts'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health/live'
      appSettings: [
        { name: 'NODE_ENV', value: 'production' }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'true' }
        { name: 'NPM_CONFIG_INCLUDE', value: 'dev' }
        { name: 'BLOB_CONTAINER_URL', value: '${storage.properties.primaryEndpoints.blob}${stateContainer.name}' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'OTEL_SERVICE_NAME', value: 'agents-sdk-production-reference' }
        { name: 'connections__serviceConnection__settings__clientId', value: agentIdentity.properties.clientId }
        { name: 'connections__serviceConnection__settings__tenantId', value: agentIdentity.properties.tenantId }
        { name: 'connections__serviceConnection__settings__authType', value: 'UserManagedIdentity' }
        { name: 'connections__serviceConnection__settings__validateIssuer', value: 'true' }
        { name: 'connectionsMap__0__connection', value: 'serviceConnection' }
        { name: 'connectionsMap__0__serviceUrl', value: '*' }
        { name: 'connectionsMap__0__audience', value: agentIdentity.properties.clientId }
        { name: 'OutboundHostValidator__Enabled', value: 'true' }
        { name: 'OutboundHostValidator__IncludeDefaultMicrosoftHosts', value: 'false' }
        { name: 'OutboundHostValidator__Hosts', value: 'webchat.botframework.com' }
      ]
    }
  }
}

resource bot 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botName
  location: 'global'
  kind: 'azurebot'
  sku: { name: botSkuName }
  properties: {
    displayName: appName
    endpoint: 'https://${app.properties.defaultHostName}/api/messages'
    msaAppId: agentIdentity.properties.clientId
    msaAppMSIResourceId: agentIdentity.id
    msaAppTenantId: agentIdentity.properties.tenantId
    msaAppType: 'UserAssignedMSI'
    publicNetworkAccess: 'Enabled'
  }
}

resource webChat 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: bot
  name: 'WebChatChannel'
  location: 'global'
  properties: {
    channelName: 'WebChatChannel'
    properties: {
      sites: [
        {
          siteName: 'Default Site'
          isEnabled: true
          isV1Enabled: false
          isV3Enabled: true
          isBlockUserUploadEnabled: true
        }
      ]
    }
  }
}

resource blobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, agentIdentity.id, 'Storage Blob Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: agentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output appUrl string = 'https://${app.properties.defaultHostName}'
output webAppName string = app.name
output botName string = bot.name
output storageAccountName string = storage.name
output agentClientId string = agentIdentity.properties.clientId
output agentTenantId string = agentIdentity.properties.tenantId

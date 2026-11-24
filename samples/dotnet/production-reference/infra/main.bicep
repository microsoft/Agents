// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

@description('Globally unique App Service and storage name prefix.')
param appName string
param location string = resourceGroup().location
param skuName string = 'B1'
@allowed(['F0', 'S1'])
param botSkuName string = 'F0'

module infrastructure '../../../shared/production-reference/infra/main.bicep' = {
  name: '${appName}-production-reference'
  params: {
    appName: appName
    location: location
    skuName: skuName
    botSkuName: botSkuName
    language: 'dotnet'
  }
}

output appUrl string = infrastructure.outputs.appUrl
output webAppName string = infrastructure.outputs.webAppName
output botName string = infrastructure.outputs.botName
output storageAccountName string = infrastructure.outputs.storageAccountName
output agentClientId string = infrastructure.outputs.agentClientId
output agentTenantId string = infrastructure.outputs.agentTenantId

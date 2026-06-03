// Provisions an Azure Storage Account with a blob container for local agent testing.
//
// Authentication: RBAC only (no shared keys exposed). The deployer is granted
// "Storage Blob Data Contributor" so DefaultAzureCredential (Azure CLI) works locally.

@description('Storage account name (3-24 chars, lowercase alphanumeric).')
@minLength(3)
@maxLength(24)
param name string

@description('Azure region.')
param location string

@description('Name of the blob container to create.')
param containerName string = 'agent-state'

@description('Principal ID of the deployer. Grants Storage Blob Data Contributor for local DefaultAzureCredential access.')
param deployerPrincipalId string = ''

@description('Principal ID of the agent service principal. Grants Storage Blob Data Contributor so the agent authenticates via AZURE_CLIENT_ID/SECRET in both local and CI.')
param agentPrincipalId string = ''

param tags object = {}

// Storage Blob Data Contributor
var blobContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
  tags: tags
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}

resource deployerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  scope: storageAccount
  name: guid(storageAccount.id, deployerPrincipalId, blobContributorRoleId)
  properties: {
    principalId: deployerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobContributorRoleId)
  }
}

resource agentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(agentPrincipalId)) {
  scope: storageAccount
  name: guid(storageAccount.id, agentPrincipalId, blobContributorRoleId)
  properties: {
    principalId: agentPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobContributorRoleId)
  }
}

output accountName string = storageAccount.name
output accountUrl string = storageAccount.properties.primaryEndpoints.blob
output containerName string = container.name

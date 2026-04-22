// Provisions a serverless Cosmos DB account with a SQL database and container.
//
// Authentication: Cosmos DB data-plane RBAC only (local auth key disabled).
// The deployer is granted "Cosmos DB Built-in Data Contributor" so
// DefaultAzureCredential (Azure CLI) works locally.

@description('Cosmos DB account name (3-44 chars, lowercase alphanumeric and hyphens).')
@minLength(3)
@maxLength(44)
param name string

@description('Azure region.')
param location string

@description('SQL database name.')
param databaseName string = 'agent-state'

@description('SQL container name.')
param containerName string = 'conversations'

@description('Principal ID of the deployer. Grants Cosmos DB Built-in Data Contributor for local DefaultAzureCredential access.')
param deployerPrincipalId string = ''

@description('Principal ID of the agent service principal. Grants Cosmos DB Built-in Data Contributor so the agent authenticates via AZURE_CLIENT_ID/SECRET in both local and CI.')
param agentPrincipalId string = ''

param tags object = {}

// Cosmos DB Built-in Data Contributor role definition ID
var cosmosDataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: name
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [{ name: 'EnableServerless' }]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    locations: [{ locationName: location, failoverPriority: 0, isZoneRedundant: false }]
    // Key-based auth disabled — RBAC only.
    disableLocalAuth: true
  }
  tags: tags
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: { paths: ['/id'], kind: 'Hash', version: 2 }
    }
  }
}

resource deployerRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (!empty(deployerPrincipalId)) {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, deployerPrincipalId, cosmosDataContributorRoleId)
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    principalId: deployerPrincipalId
    scope: cosmosAccount.id
  }
}

resource agentRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (!empty(agentPrincipalId)) {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, agentPrincipalId, cosmosDataContributorRoleId)
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    principalId: agentPrincipalId
    scope: cosmosAccount.id
  }
}

output accountName string = cosmosAccount.name
output endpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = database.name
output containerName string = container.name

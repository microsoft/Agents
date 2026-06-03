@description('Key Vault name. Maximum 24 characters.')
@maxLength(24)
param name string

@description('Azure region.')
param location string

@description('Principal ID of the deployer (user or service principal) that writes the client secret at provision time.')
param deployerPrincipalId string = ''

param tags object = {}

var kvSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Officer

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
  tags: tags
}

// Deployer can write the client secret during provisioning.
resource deployerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  scope: kv
  name: guid(kv.id, deployerPrincipalId, kvSecretsOfficerRoleId)
  properties: {
    principalId: deployerPrincipalId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsOfficerRoleId)
  }
}

output name string = kv.name
output uri string = kv.properties.vaultUri

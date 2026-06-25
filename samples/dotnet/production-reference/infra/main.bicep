// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Production Reference — Azure infrastructure for the Agents SDK production-reference sample.
// Deploys: App Service, Storage, Key Vault, Application Insights, Log Analytics, and
// role assignments that enable the Web App managed identity to access blob storage and Key Vault.
//
// Prerequisites: an Azure Bot Service registration already exists. Pass its client ID and client
// secret via the botClientId and botClientSecret parameters.  Bot Service messaging endpoint
// configuration is manual (see DEPLOYMENT.md).

// ── Parameters ─────────────────────────────────────────────────────────────────

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short prefix used to derive resource names. Lowercase letters, digits, and hyphens; 2–8 characters.')
@minLength(2)
@maxLength(8)
param namePrefix string

@description('Client ID (App ID) of the existing Azure Bot Service registration.')
param botClientId string

@description('Client secret of the existing Azure Bot Service registration. Stored in Key Vault; never written to app settings in plain text.')
@secure()
param botClientSecret string

@description('Tenant ID of the Entra directory that owns the Bot registration.')
param tenantId string

@description('Entra authority endpoint. Defaults to the public cloud endpoint for the given tenant.')
param authorityEndpoint string = '${environment().authentication.loginEndpoint}${tenantId}'

// ── Derived names ──────────────────────────────────────────────────────────────

var suffix = uniqueString(resourceGroup().id, namePrefix)

// Storage account: 3–24 chars, lowercase alphanumeric only.
var storageAccountName = take(toLower(replace('${namePrefix}${suffix}', '-', '')), 24)

// Key Vault: 3–24 chars, alphanumeric and hyphens.
var keyVaultName = take('${namePrefix}-kv-${suffix}', 24)

var appServicePlanName = '${namePrefix}-plan'
var webAppName = '${namePrefix}-app-${take(suffix, 8)}'
var logAnalyticsName = '${namePrefix}-law'
var appInsightsName = '${namePrefix}-ai'
var blobContainerName = 'agents-state'

// ── Built-in role definition IDs ───────────────────────────────────────────────

// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/storage#storage-blob-data-contributor
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/security#key-vault-secrets-user
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Log Analytics workspace ────────────────────────────────────────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ── Application Insights ───────────────────────────────────────────────────────

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ── Storage account + blob container ──────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: blobContainerName
  properties: {
    publicAccess: 'None'
  }
}

// ── App Service plan ───────────────────────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// ── Web App (system-assigned managed identity) ─────────────────────────────────
// App settings are defined in a separate resource below so they can depend on
// the Key Vault secret and role assignments being in place before the app
// attempts to resolve Key Vault references at startup.

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      healthCheckPath: '/health/live'
      alwaysOn: true
    }
  }
}

// ── Key Vault ──────────────────────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource botClientSecretKvSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'BotClientSecret'
  properties: {
    value: botClientSecret
  }
}

// ── Role assignment: Storage Blob Data Contributor on blob container ───────────

resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(blobContainer.id, webApp.id, storageBlobDataContributorRoleId)
  scope: blobContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Role assignment: Key Vault Secrets User on Key Vault ──────────────────────

resource keyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── App settings ───────────────────────────────────────────────────────────────
// Defined as a separate child resource so Bicep generates an explicit dependsOn
// on the KV secret and both role assignments. This ensures the managed identity
// has Key Vault Secrets User access before the app resolves KV references at
// startup.

resource webAppSettings 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: webApp
  name: 'appsettings'
  dependsOn: [
    keyVaultSecretsUserRoleAssignment
    storageBlobDataContributorRoleAssignment
  ]
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    WEBSITE_RUN_FROM_PACKAGE: '1'
    WEBSITE_ENABLE_SYNC_UPDATE_SITE: 'true'

    APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString

    AZURE_BLOB_STORAGE_CONTAINER_URI: '${storageAccount.properties.primaryEndpoints.blob}${blobContainerName}'

    TokenValidation__Enabled: 'true'
    TokenValidation__Audiences__0: botClientId
    TokenValidation__TenantId: tenantId

    Connections__ServiceConnection__Settings__AuthType: 'ClientSecret'
    Connections__ServiceConnection__Settings__AuthorityEndpoint: authorityEndpoint
    Connections__ServiceConnection__Settings__ClientId: botClientId
    Connections__ServiceConnection__Settings__ClientSecret: '@Microsoft.KeyVault(SecretUri=${botClientSecretKvSecret.properties.secretUriWithVersion})'

    ConnectionsMap__0__ServiceUrl: '*'
    ConnectionsMap__0__Connection: 'ServiceConnection'
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────────

@description('Default host name of the deployed App Service (e.g. myapp.azurewebsites.net).')
output appServiceDefaultHostName string = webApp.properties.defaultHostName

@description('Messaging endpoint URL to register in Azure Bot Service.')
output messagingEndpoint string = 'https://${webApp.properties.defaultHostName}/api/messages'

@description('Full URI of the blob container used for agent state storage.')
output storageContainerUri string = '${storageAccount.properties.primaryEndpoints.blob}${blobContainerName}'

@description('Name of the deployed Key Vault.')
output keyVaultName string = keyVault.name


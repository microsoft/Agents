extension microsoftGraphV1

@description('Base name used for the app displayName and uniqueName (e.g. "bot-e2e-local").')
param botName string

// No UMI/FIC for the local environment — agents authenticate via client secret.

var appDisplayName = '${botName}-app'

resource app 'Microsoft.Graph/applications@v1.0' = {
  displayName: appDisplayName
  uniqueName: appDisplayName
  signInAudience: 'AzureADMyOrg'
  owners: {
    relationships: [deployer().objectId]
  }
}

resource sp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: app.appId
  accountEnabled: true
  servicePrincipalType: 'Application'
}

output appId string = app.appId
output tenantId string = tenant().tenantId

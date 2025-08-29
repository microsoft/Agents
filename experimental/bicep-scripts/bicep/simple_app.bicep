extension microsoftGraphV1

param botName string

resource app 'Microsoft.Graph/applications@v1.0' = {
  displayName: '${botName}-app'
  uniqueName: '${botName}-app'
  signInAudience: 'AzureADMyOrg'
  owners: {
    relationships: [ deployer().objectId ]
  }
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: app.appId
  accountEnabled: true
  servicePrincipalType: 'Application'
}

output appId string = app.appId

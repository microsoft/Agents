extension microsoftGraphV1

// Creates the A365 CLI custom client app registration.
//
// What bicep configures here:
//   - Public client with isFallbackPublicClient = true
//   - Redirect URIs: http://localhost:8400/ and http://localhost
//   - Visible Microsoft Graph delegated permissions (portal-visible, v1.0 scope IDs):
//       User.Read, User.ReadWrite.All, Directory.Read.All,
//       DelegatedPermissionGrant.ReadWrite.All
//
// What pre_setup_a365.ps1 must add afterward (cannot be set in bicep):
//   - WAM redirect URI: ms-appx-web://Microsoft.AAD.BrokerPlugin/{client-id}
//     (requires the app's own client ID — circular reference in bicep)
//   - Beta AgentIdentity*/AgentInstance*/AgentRegistration* Graph scopes
//     (not available in the Graph v1.0 bicep extension)
//   - Tenant-wide admin consent for all 14 permissions
//     (User.ReadWrite.All consent requires Global Administrator)

@description('Unique name suffix to avoid conflicts across environments in the same tenant.')
param environmentName string

// Well-known Microsoft Graph scope IDs for delegated permissions.
var msGraphAppId = '00000003-0000-0000-c000-000000000000'
var userReadScopeId           = 'e1fe6dd8-ba31-4d61-89e7-88639da4683d'
var userReadWriteAllScopeId   = '204e0828-b5ca-4ad8-b9f3-f32a958e7cc4'
var directoryReadAllScopeId   = '7ab1d382-f21e-4acd-a863-ba3e13f7da61'
var delegatedPermGrantRWScopeId = '41ce6ca6-6826-4807-84f1-1c82854f7af5'

resource app 'Microsoft.Graph/applications@v1.0' = {
  // 'Agent 365 CLI' enables auto-lookup when running `a365 setup all` without explicit --client-id.
  displayName: 'Agent 365 CLI'
  uniqueName: 'agent-365-cli-${toLower(environmentName)}'
  signInAudience: 'AzureADMyOrg'
  isFallbackPublicClient: true
  publicClient: {
    redirectUris: [
      'http://localhost:8400/'
      'http://localhost'
    ]
  }
  requiredResourceAccess: [
    {
      resourceAppId: msGraphAppId
      resourceAccess: [
        { id: userReadScopeId,             type: 'Scope' }
        { id: userReadWriteAllScopeId,     type: 'Scope' }
        { id: directoryReadAllScopeId,     type: 'Scope' }
        { id: delegatedPermGrantRWScopeId, type: 'Scope' }
      ]
    }
  ]
  owners: {
    relationships: [deployer().objectId]
  }
}

output appId string = app.appId
output objectId string = app.id

extension microsoftGraphV1

param endpoint string
param botName string

@allowed([
  'aadv2'
])
param oauthType string = 'aadv2' // for later
var appName = '${botName}-oauth-app'

// for when creating a brand new app registration
// we need to be able to access the app ID
resource appRegistrationBase 'Microsoft.Graph/applications@v1.0' = {
  displayName: appName
  uniqueName: appName
  signInAudience: 'AzureADMyOrg'
  owners: {
    relationships: [ deployer().objectId ]
  }

  // Corresponds to "Authentication" section
  web: {
    homePageUrl: '${endpoint}/api/messages'
    implicitGrantSettings: {
      enableAccessTokenIssuance: true
      enableIdTokenIssuance: true
    }
    redirectUris: [
      'https://token.botframework.com/.auth/web/redirect'
    ]
  }
  publicClient: {
    redirectUris: [
      'http://localhost'
      'msal79e090f7-bb8e-4b24-b966-1e88178962c6://auth'
      'https://login.live.com/oauth20_desktop.srf'
      '${environment().authentication.loginEndpoint}common/oauth2/nativeclient'
    ]
  }
  isFallbackPublicClient: true // idk what this does

  // Corresponds to "Expose an API" section
  api: {
    oauth2PermissionScopes: [
      {
        adminConsentDescription: 'defaultScope'
        adminConsentDisplayName: 'defaultScope'
        id: guid(resourceGroup().id, botName, 'defaultScope')
        isEnabled: true
        type: 'User'
        userConsentDescription: 'Allows the bot to access your data.'
        userConsentDisplayName: 'Access your data'
        value: 'defaultScope'
      }
    ]
    preAuthorizedApplications: [
      {
        appId: '5e3ce6c0-2b1f-4285-8d4b-75ee78787346' // Teams web application
        delegatedPermissionIds: [
          guid(resourceGroup().id, botName, 'defaultScope')
        ]
      }
      {
        appId: '1fec8e78-bce4-4aaf-ab1b-5451cc387264' // Teams mobile/desktop application
        delegatedPermissionIds: [
          guid(resourceGroup().id, botName, 'defaultScope')
        ]
      }
    ]
  }

  // Corresponds to "API permmissions" section
  requiredResourceAccess: [
    {
      resourceAppId: '8578e004-a5c6-46e7-913e-12f58912df43' // Power Platform API
      resourceAccess: [
        {
          id: '204440d3-c1d0-4826-b570-99eb6f5e2aeb' // CopilotStudio.Copilots.Invoke
          type: 'Scope'
        }
      ]
    }
    {
      resourceAppId: '00000003-0000-0000-c000-000000000000' // Microsoft Graph
      resourceAccess: [
        {
          id: 'e1fe6dd8-ba31-4d61-89e7-88639da4683d' // User.Read
          type: 'Scope'
        }
        {
          id: '14dad69e-099b-42c9-810b-d002981feec1' // profile
          type: 'Scope'
        }
        {
          id: '37f7f235-527c-4136-accd-4a02d197296e' // openid
          type: 'Scope'
        }
      ]
    }
    {
      resourceAppId: '00000007-0000-0000-c000-000000000000' // Dynamics CRM
      resourceAccess: [
        {
          id: '78ce3f0f-a1ce-49c2-8cde-64b5c0896db4' // user_impersonation
          type: 'Scope'
        }
      ]
    }
  ]
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: appRegistrationBase.appId
  accountEnabled: true
  servicePrincipalType: 'Application'
}

// use existing app ID to set up API
// should overwrite existing app registration
resource appRegistration 'Microsoft.Graph/applications@v1.0' = {
  displayName: appName
  uniqueName: appName
  signInAudience: appRegistrationBase.signInAudience
  web: appRegistrationBase.web
  publicClient: appRegistrationBase.publicClient
  isFallbackPublicClient: appRegistrationBase.isFallbackPublicClient
  api: appRegistrationBase.api
  requiredResourceAccess: appRegistrationBase.requiredResourceAccess

  // Application ID URI from "Expose an API"
  identifierUris: [
    'api://botid-${appRegistrationBase.appId}'
  ]
}

output appId string = appRegistration.appId

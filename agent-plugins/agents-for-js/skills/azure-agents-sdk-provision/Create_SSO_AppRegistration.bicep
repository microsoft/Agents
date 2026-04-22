// =============================================================================
// Create_SSO_AppRegistration.bicep
// Phase 1 — Creates the base Entra ID App Registration for an Azure Bot Agent with SSO configuration for basic graph authentication 
// with Teams SSO support.
//
//
// Requirements:
//   - Bicep CLI 0.26.0+
//   - Microsoft Graph Bicep extension enabled
//
// Output example:
//   newAppId: The appId of the newly created App Registration
//   newAppObjectId: The objectId of the newly created App Registration
// =============================================================================

extension microsoftGraph

// -------------------------------------------------------
// Parameters
// -------------------------------------------------------
// NAME OF THE APP REGISTRATION
param APP_NAME string
// OBJECT ID OF THE OWNER OF THE APP REGISTRATION, THIS SHOULD BE THE CREATING USER'S OBJECT ID
param OWNER_OBJECT_ID string
// GENERATED OAUTH SCOPE ID FOR PRE-AUTHORIZATION (GUID FORMAT)
param OAUTH_SCOPE_ID string
// REDIRECT URI FOR THE AZURE BOT SERVICE (defaulted for all commerical regions)
param ABS_REDIRECTURI string = 'https://token.botframework.com/.auth/web/redirect'

// -------------------------------------------------------
// App Registration
// -------------------------------------------------------
resource botApp 'Microsoft.Graph/applications@v1.0' = {
  // uniqueName makes this resource idempotent and referenceable in Phase 2
  uniqueName: APP_NAME
  displayName: APP_NAME
  signInAudience: 'AzureADMyOrg'

  // identifierUris intentionally empty at creation — set in Phase 2
  identifierUris: []

  web: {
    redirectUris: [
      ABS_REDIRECTURI
    ]
    implicitGrantSettings: {
      enableAccessTokenIssuance: false
      enableIdTokenIssuance: false
    }
  }

  owners: {
    relationships: [
      OWNER_OBJECT_ID
    ]
  }

  // Microsoft Graph delegated permissions to support user login, Additional permissions can be added as needed to support other Graph API calls
  requiredResourceAccess: [
    {
      resourceAppId: '00000003-0000-0000-c000-000000000000' // Microsoft Graph
      resourceAccess: [
        { id: 'e1fe6dd8-ba31-4d61-89e7-88639da4683d', type: 'Scope' } // User.Read
        { id: '37f7f235-527c-4136-accd-4a02d197296e', type: 'Scope' } // openid
        { id: '14dad69e-099b-42c9-810b-d002981feec1', type: 'Scope' } // profile
      ]
    }
  ]
}

// -------------------------------------------------------
// Update App Registration with API configuration for Bot Framework / Teams SSO
// This will update the App Registration with the required API configuration for Bot Framework / Teams SSO
// -------------------------------------------------------

resource botAppUp1 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: APP_NAME
  displayName: APP_NAME

  // api://botid-{appId} is required for Bot Framework / Teams SSO
  identifierUris: [
    'api://botid-${botApp.appId}'
  ]

  api: {
    // Re-specify scope to preserve it during the in-place update
    oauth2PermissionScopes: [
      {
        id: OAUTH_SCOPE_ID
        adminConsentDescription: 'Allow the app to access the agent on behalf of the signed-in user.'
        adminConsentDisplayName: 'Access the agent as a user'
        isEnabled: true
        type: 'User'
        userConsentDescription: 'Allow this app to access the agent on your behalf.'
        userConsentDisplayName: 'Access the agent as a user'
        value: 'access_as_user'
      }
    ]
  }
}

// -------------------------------------------------------
// Outputs
// -------------------------------------------------------
output newAppId string = botApp.appId
output newAppObjectId string = botApp.id





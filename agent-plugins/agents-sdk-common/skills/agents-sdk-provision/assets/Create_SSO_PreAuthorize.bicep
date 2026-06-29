// =============================================================================
// Create_SSO_PreAuthorize.bicep
// Phase 2 — Updates the base Entra ID App Registration to add Preauthorized Applications to the API configuration to support Teams SSO
//
// Requirements:
//   - Bicep CLI 0.26.0+
//   - Microsoft Graph Bicep extension enabled
//
// =============================================================================

extension microsoftGraph

// -------------------------------------------------------
// Parameters
// -------------------------------------------------------
// NAME OF THE APP REGISTRATION
param APP_NAME string
// GENERATED OAUTH SCOPE ID FOR PRE-AUTHORIZATION (GUID FORMAT)
param OAUTH_SCOPE_ID string

// -------------------------------------------------------
// Update App Registration with Preauthorized Applications for Teams SSO
// -------------------------------------------------------
resource botAppUp3 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: APP_NAME
  displayName: APP_NAME

  api: {
    // Pre-authorized Microsoft first-party Teams/Office clients
    preAuthorizedApplications: [
      // Microsoft Teams (web)
      { appId: '4345a7b9-9a63-4910-a426-35363201d503', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Office 365 / Outlook Online
      { appId: '00000002-0000-0ff1-ce00-000000000000', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Microsoft Teams (mobile/desktop)
      { appId: '27922004-5251-4030-b22d-91ecd9a37ea4', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Microsoft 365 web
      { appId: '4765445b-32c6-49b0-83e6-1d93765276ca', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Office UWP / PWA
      { appId: '0ec893e0-5785-4de6-99da-4ed124e5296c', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Teams web app
      { appId: 'bc59ab01-8403-45c6-8796-ac3ef710b3e3', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Microsoft Office (desktop)
      { appId: 'd3590ed6-52b3-4102-aeff-aad2292ab01c', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Teams mobile client
      { appId: '5e3ce6c0-2b1f-4285-8d4b-75ee78787346', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
      // Teams mobile app
      { appId: '1fec8e78-bce4-4aaf-ab1b-5451cc387264', delegatedPermissionIds: [OAUTH_SCOPE_ID] }
    ]
  }
}


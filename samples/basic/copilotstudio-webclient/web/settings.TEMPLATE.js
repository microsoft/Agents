/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

// Flag to enable debug mode, which will store the debug information in localStorage.
// Copilot Studio Client uses the "debug" library for logging (https://github.com/debug-js/debug?tab=readme-ov-file#browser-support).
window.localStorage.debug = 'copilot-studio-client'

export const settings = {
  // App ID of the App Registration used to log in, this should be in the same tenant as the Copilot.
  appClientId: '',
  // Tenant ID of the App Registration used to log in, this should be in the same tenant as the Copilot.
  tenantId: '',
  // Environment ID of the environment with the Copilot Studio App.
  environmentId: '',
  // Schema Name of the Copilot to use.
  agentIdentifier: '',
  // PowerPlatformCloud enum key.
  cloud: '',
  // Power Platform API endpoint to use if Cloud is configured as "Other".
  customPowerPlatformCloud: '',
  // AgentType enum key.
  copilotAgentType: '',
  // URL used to connect to the Copilot Studio service.
  // directConnectUrl: 'https://dbe9da0559b6ea34b8c95474ae0402.16.environment.api.powerplatform.com/copilotstudio/dataverse-backed/authenticated/bots/cr761_agentsSdkDev/conversations?api-version=2022-03-01-preview',
  // Flag to use the "x-ms-d2e-experimental" header URL on subsequent calls to the Copilot Studio service.
  useExperimentalEndpoint: false,
}

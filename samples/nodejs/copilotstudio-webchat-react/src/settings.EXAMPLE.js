/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

// This file emulates the process object in node.
// rename this file to settings.js before running this test sample
import { ConnectionSettings } from '@microsoft/agents-copilotstudio-client'

// Flag to enable debug mode, which will store the debug information in localStorage.
// Copilot Studio Client uses the "debug" library for logging (https://github.com/debug-js/debug?tab=readme-ov-file#browser-support).
window.localStorage.debug = 'copilot-studio:*'

export class SampleConnectionSettings extends ConnectionSettings {
  constructor () {
    super({
      // Environment ID of the Copilot Studio App (required if directConnectUrl is empty).
      environmentId: '',
      // Schema Name of the Copilot Studio App (required if directConnectUrl is empty).
      schemaName: '',
      // URL used to connect to the Copilot Studio service (use this OR environmentId + schemaName).
      directConnectUrl: '',
      // Cloud hosting the Power Platform Services. Default value is "Prod". Set to "Other" when using customPowerPlatformCloud.
      cloud: '',
      // Power Platform API endpoint to use if Cloud is configured as "Other".
      customPowerPlatformCloud: '',
      // Type of Copilot Studio Agent (Published or Prebuilt). Default value is "Published".
      copilotAgentType: '',
      // Flag to use the "x-ms-d2e-experimental" header URL on subsequent calls to the Copilot Studio service.
      useExperimentalEndpoint: false
    })
    // App ID of the App Registration used to log in; should be in the same tenant as the Copilot Studio agent.
    this.appClientId = ''
    // Tenant ID of the App Registration used to log in; must match the Copilot’s tenant.
    this.tenantId = ''
    // Authority endpoint for the Azure AD login. Default is 'https://login.microsoftonline.com'.
    this.authority = ''
  }
}

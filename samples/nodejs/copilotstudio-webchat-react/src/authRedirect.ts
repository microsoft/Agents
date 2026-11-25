/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import { broadcastResponseToMainFrame } from '@azure/msal-browser/redirect-bridge'

broadcastResponseToMainFrame().catch((error: unknown) => {
  console.error('Unable to complete Microsoft sign-in.', error)
  const root = document.getElementById('root')
  if (root) {
    root.textContent = 'Microsoft sign-in could not complete. Return to Web Chat and try again.'
  }
})

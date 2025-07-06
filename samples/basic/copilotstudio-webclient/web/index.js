/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import {
  CopilotStudioClient,
  CopilotStudioWebChat,
} from '@microsoft/agents-copilotstudio-client'

import { acquireToken } from './acquireToken.js'
import { settings } from './settings.js'

try {
  const token = await acquireToken(settings)
  const client = new CopilotStudioClient(settings, token)

  window.WebChat.renderWebChat(
    {
      directLine: CopilotStudioWebChat.createConnection(client, { typingIndicator: true }),
    },
    document.getElementById('webchat')
  )

  document.querySelector('#webchat > *').focus()
} catch (err) {
  console.error('Failed to initialize:', err)
}

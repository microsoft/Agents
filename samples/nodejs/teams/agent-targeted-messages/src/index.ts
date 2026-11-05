// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { startServer } from '@microsoft/agents-hosting-express'
import { createAgent, normalizeIncoming } from './agent.js'

startServer(createAgent(), {
  beforeListen: (server) => {
    server.use('/api/messages', (request, _response, next) => {
      if (request.body && typeof request.body === 'object') {
        normalizeIncoming(request.body)
      }
      next()
    })
    server.get('/', (_request, response) => response.status(200).send('Teams Targeted Messages Agent'))
  },
})

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { startServer } from '@microsoft/agents-hosting-express'
import { createAgent } from './agent.js'

startServer(createAgent(), {
  beforeListen: (server) => {
    server.get('/', (_request, response) => response.status(200).send('bot-attachments Agent'))
  },
})

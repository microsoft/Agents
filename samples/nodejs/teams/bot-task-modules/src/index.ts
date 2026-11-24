// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { startServer } from '@microsoft/agents-hosting-express'
import { createAgent } from './agent.js'
import { fileURLToPath } from 'node:url'

startServer(createAgent(), {
  beforeListen: (server) => {
    server.get('/', (_request, response) => response.status(200).send('bot-task-modules Agent'))
    server.get('/CustomForm/', (_request, response) => {
      response.sendFile(fileURLToPath(new URL('../pages/CustomForm/index.html', import.meta.url)))
    })
  },
})

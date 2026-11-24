// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { createAgent } from './agent.js'
import { loadConfig } from './config.js'
import { createServer, listen, closeServer } from './server.js'
import { createStorage } from './storage.js'
import { shutdownTelemetry } from './telemetry.js'

const config = loadConfig()
const storage = createStorage(config)
const agent = createAgent(config, storage)
const server = listen(createServer(agent, storage), config.port)

let stopping = false
async function shutdown (signal: string): Promise<void> {
  if (stopping) return
  stopping = true
  console.info(`Received ${signal}; draining HTTP traffic.`)
  try {
    await closeServer(server)
    await shutdownTelemetry()
    process.exitCode = 0
  } catch {
    process.exitCode = 1
  }
}

process.once('SIGTERM', () => { shutdown('SIGTERM').catch(() => { process.exitCode = 1 }) })
process.once('SIGINT', () => { shutdown('SIGINT').catch(() => { process.exitCode = 1 }) })

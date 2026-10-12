// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { createServer as createHttpServer } from 'node:http'
import test from 'node:test'
import { MemoryStorage, type Storage } from '@microsoft/agents-hosting'
import { loadConfig } from '../src/config.js'
import { createAgent } from '../src/agent.js'
import { createServer, isAllowedActivityServiceUrl, isTrustedIssuer, isWebChatServiceUrl, usesNativeIssuerValidation } from '../src/server.js'

class UnavailableStorage implements Pick<Storage, 'write' | 'delete'> {
  async write (): Promise<never> {
    throw new Error('storage unavailable')
  }

  async delete (): Promise<void> {}
}

test('issuer authorization requires an exact configured issuer', () => {
  const trusted = ['https://api.botframework.com', 'https://login.microsoftonline.com/tenant-id/v2.0']
  assert.equal(isTrustedIssuer('https://api.botframework.com', trusted), true)
  assert.equal(isTrustedIssuer('https://login.microsoftonline.com/other-tenant/v2.0', trusted), false)
  assert.equal(isTrustedIssuer(undefined, trusted), false)
})

test('issuer fallback yields to SDK-native issuer validation', () => {
  assert.equal(usesNativeIssuerValidation({ validateIssuer: true } as never), true)
  assert.equal(usesNativeIssuerValidation({} as never), false)
})

test('service URL policy allows local development and requires the exact production host', () => {
  assert.equal(isWebChatServiceUrl('https://webchat.botframework.com/'), true)
  assert.equal(isWebChatServiceUrl('https://subdomain.webchat.botframework.com/'), false)
  assert.equal(isWebChatServiceUrl('https://webchat.botframework.com.example/'), false)
  assert.equal(isWebChatServiceUrl('not-a-url'), false)
  assert.equal(isAllowedActivityServiceUrl('http://localhost:3978/', false), true)
  assert.equal(isAllowedActivityServiceUrl('http://127.0.0.1:3978/', false), true)
  assert.equal(isAllowedActivityServiceUrl('https://example.com/', false), false)
  assert.equal(isAllowedActivityServiceUrl('http://localhost:3978/', true), false)
})

async function withHttpServer (storage: Pick<Storage, 'write' | 'delete'>, run: (baseUrl: string) => Promise<void>): Promise<void> {
  const original = { ...process.env }
  process.env.NODE_ENV = 'development'
  process.env.BLOB_STORAGE_CONNECTION_STRING = 'UseDevelopmentStorage=true'
  const config = loadConfig()
  const agent = createAgent(config, new MemoryStorage())
  const server = createHttpServer(createServer(agent, storage))
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve))
  const address = server.address()
  assert.ok(address && typeof address !== 'string')
  try {
    await run(`http://127.0.0.1:${address.port}`)
  } finally {
    await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))
    process.env = original
  }
}

test('liveness is independent of dependencies', async () => {
  await withHttpServer(new UnavailableStorage(), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/health/live`)
    assert.equal(response.status, 200)
  })
})

test('readiness reports an unavailable dependency', async () => {
  await withHttpServer(new UnavailableStorage(), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/health/ready`)
    assert.equal(response.status, 503)
    assert.deepEqual(await response.json(), { status: 'not ready' })
  })
})

test('message endpoint rejects an oversized payload', async () => {
  await withHttpServer(new UnavailableStorage(), async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/messages`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ text: 'x'.repeat(300 * 1024) }),
    })
    assert.equal(response.status, 413)
    assert.deepEqual(await response.json(), { error: 'Request could not be processed.' })
  })
})

test('production message endpoint authenticates before channel authorization', async () => {
  const original = { ...process.env }
  process.env.NODE_ENV = 'production'
  process.env.TEST_MODE = 'true'
  process.env.BLOB_CONTAINER_URL = 'https://state.blob.core.windows.net/container'
  process.env.APPLICATIONINSIGHTS_CONNECTION_STRING = 'InstrumentationKey=test'
  process.env.connections__serviceConnection__settings__clientId = 'agent-app-id'
  process.env.connections__serviceConnection__settings__tenantId = 'tenant-id'
  process.env.connections__serviceConnection__settings__authType = 'UserManagedIdentity'
  process.env.connections__serviceConnection__settings__validateIssuer = 'true'
  process.env.connectionsMap__0__connection = 'serviceConnection'
  process.env.connectionsMap__0__audience = 'agent-app-id'
  process.env.connectionsMap__0__serviceUrl = '*'
  process.env.OutboundHostValidator__Enabled = 'true'
  process.env.OutboundHostValidator__IncludeDefaultMicrosoftHosts = 'false'
  process.env.OutboundHostValidator__Hosts = 'webchat.botframework.com'

  const config = loadConfig()
  const agent = createAgent(config, new MemoryStorage())
  const server = createHttpServer(createServer(agent, new UnavailableStorage()))
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve))
  const address = server.address()
  assert.ok(address && typeof address !== 'string')
  try {
    const response = await fetch(`http://127.0.0.1:${address.port}/api/messages`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: '{}',
    })
    assert.equal(response.status, 401)

    const malformedHeader = await fetch(`http://127.0.0.1:${address.port}/api/messages`, {
      method: 'POST',
      headers: { authorization: 'Basic invalid', 'content-type': 'application/json' },
      body: '{}',
    })
    assert.equal(malformedHeader.status, 401)

    const wrongChannel = await fetch(`http://127.0.0.1:${address.port}/api/messages`, {
      method: 'POST',
      headers: { authorization: 'Bearer invalid', 'content-type': 'application/json' },
      body: JSON.stringify({ serviceUrl: 'https://example.com/' }),
    })
    assert.equal(wrongChannel.status, 401)

    const invalidToken = await fetch(`http://127.0.0.1:${address.port}/api/messages`, {
      method: 'POST',
      headers: { authorization: 'Bearer invalid', 'content-type': 'application/json' },
      body: JSON.stringify({
        type: 'message',
        id: 'activity-id',
        serviceUrl: 'https://webchat.botframework.com/',
        channelId: 'webchat',
        from: { id: 'user-id' },
        recipient: { id: 'agent-id' },
        conversation: { id: 'conversation-id' },
        text: 'Service is down',
      }),
    })
    assert.equal(invalidToken.status, 401)
  } finally {
    await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))
    process.env = original
  }
})

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import test from 'node:test'
import { loadConfig } from '../src/config.js'

function setValidProductionEnvironment (): void {
  process.env.NODE_ENV = 'production'
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
}

test('production configuration fails without a Blob container URL', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    delete process.env.BLOB_CONTAINER_URL
    assert.throws(loadConfig, /BLOB_CONTAINER_URL/)
  } finally {
    process.env = original
  }
})

test('production configuration rejects a non-agent audience', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    process.env.connectionsMap__0__audience = 'other-app-id'
    assert.throws(loadConfig, /must equal the agent client ID/)
  } finally {
    process.env = original
  }
})

test('production configuration requires the default connection mapping', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    process.env.connectionsMap__0__connection = 'otherConnection'
    assert.throws(loadConfig, /must be "serviceConnection"/)
  } finally {
    process.env = original
  }
})

test('production configuration requires telemetry export', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    delete process.env.APPLICATIONINSIGHTS_CONNECTION_STRING
    assert.throws(loadConfig, /APPLICATIONINSIGHTS_CONNECTION_STRING/)
  } finally {
    process.env = original
  }
})

test('production configuration requires the provisioned user-assigned identity', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    process.env.connections__serviceConnection__settings__authType = 'SystemManagedIdentity'
    assert.throws(loadConfig, /must be "UserManagedIdentity"/)
  } finally {
    process.env = original
  }
})

test('production configuration requires issuer validation', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    process.env.connections__serviceConnection__settings__validateIssuer = 'false'
    assert.throws(loadConfig, /validateIssuer must be "true"/)
  } finally {
    process.env = original
  }
})

test('production configuration requires the bounded outbound host policy', () => {
  const original = { ...process.env }
  try {
    setValidProductionEnvironment()
    process.env.OutboundHostValidator__IncludeDefaultMicrosoftHosts = 'true'
    assert.throws(loadConfig, /IncludeDefaultMicrosoftHosts must be "false"/)

    process.env.OutboundHostValidator__IncludeDefaultMicrosoftHosts = 'false'
    process.env.OutboundHostValidator__Hosts = 'example.com'
    assert.throws(loadConfig, /OutboundHostValidator__Hosts/)
  } finally {
    process.env = original
  }
})

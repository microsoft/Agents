// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

export interface AppConfig {
  readonly isProduction: boolean
  readonly port: number
  readonly blobContainerId: string
  readonly blobConnectionString?: string
  readonly blobContainerUrl?: string
  readonly agentClientId?: string
}

const sdkDefaultServiceUrl = '*'
const webChatServiceHost = 'webchat.botframework.com'

function required (name: string): string {
  const value = process.env[name]?.trim()
  if (!value) throw new Error(`${name} is required in production.`)
  return value
}

function positiveInteger (name: string, fallback: number): number {
  const value = Number.parseInt(process.env[name] ?? String(fallback), 10)
  if (!Number.isSafeInteger(value) || value <= 0) throw new Error(`${name} must be a positive integer.`)
  return value
}

export function loadConfig (): AppConfig {
  const isProduction = process.env.NODE_ENV === 'production'
  const blobContainerId = process.env.BLOB_CONTAINER_ID?.trim() || 'agents-production-reference-state'
  const blobConnectionString = process.env.BLOB_STORAGE_CONNECTION_STRING?.trim()
  const blobContainerUrl = process.env.BLOB_CONTAINER_URL?.trim()

  if (isProduction && !blobContainerUrl) required('BLOB_CONTAINER_URL')
  if (!isProduction && !blobConnectionString && !blobContainerUrl) {
    throw new Error('Set BLOB_STORAGE_CONNECTION_STRING or BLOB_CONTAINER_URL.')
  }
  if (isProduction) {
    required('APPLICATIONINSIGHTS_CONNECTION_STRING')
    const clientId = required('connections__serviceConnection__settings__clientId')
    required('connections__serviceConnection__settings__tenantId')
    if (required('connections__serviceConnection__settings__authType') !== 'UserManagedIdentity') {
      throw new Error('connections__serviceConnection__settings__authType must be "UserManagedIdentity".')
    }
    if (required('connections__serviceConnection__settings__validateIssuer').toLowerCase() !== 'true') {
      throw new Error('connections__serviceConnection__settings__validateIssuer must be "true".')
    }
    if (required('connectionsMap__0__connection') !== 'serviceConnection') {
      throw new Error('connectionsMap__0__connection must be "serviceConnection".')
    }
    const audience = required('connectionsMap__0__audience')
    if (audience !== clientId) throw new Error('connectionsMap__0__audience must equal the agent client ID.')
    if (required('connectionsMap__0__serviceUrl') !== sdkDefaultServiceUrl) {
      throw new Error('connectionsMap__0__serviceUrl must be "*" for the SDK default connection. Web Chat host enforcement is in the HTTP middleware.')
    }
    if (required('OutboundHostValidator__Enabled').toLowerCase() !== 'true') {
      throw new Error('OutboundHostValidator__Enabled must be "true".')
    }
    if (required('OutboundHostValidator__IncludeDefaultMicrosoftHosts').toLowerCase() !== 'false') {
      throw new Error('OutboundHostValidator__IncludeDefaultMicrosoftHosts must be "false" for the bounded Web Chat profile.')
    }
    if (required('OutboundHostValidator__Hosts').toLowerCase() !== webChatServiceHost) {
      throw new Error(`OutboundHostValidator__Hosts must be "${webChatServiceHost}".`)
    }
  }

  return {
    isProduction,
    port: positiveInteger('PORT', 3978),
    blobContainerId,
    blobConnectionString,
    blobContainerUrl,
    agentClientId: process.env.connections__serviceConnection__settings__clientId?.trim(),
  }
}

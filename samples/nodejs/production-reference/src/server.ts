// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import type { Server } from 'node:http'
import express, { type Express, type Request, type Response } from 'express'
import { authorizeJWT, createCloudAdapter, loadAuthConfigFromEnv, type AgentApplication, type AuthConfiguration, type Storage, type TurnState } from '@microsoft/agents-hosting'
import { recordFailure } from './telemetry.js'

const readinessKey = 'health.readiness'

export function createServer (agent: AgentApplication<TurnState>, storage: Pick<Storage, 'write' | 'delete'>): Express {
  const app = express()
  app.disable('x-powered-by')
  app.use(express.json({ limit: '256kb' }))

  app.get('/health/live', (_request: Request, response: Response) => response.status(200).json({ status: 'ok' }))
  app.get('/health/ready', async (_request: Request, response: Response) => {
    try {
      await storage.write({ [readinessKey]: { eTag: '*', checked: true } })
      await storage.delete([readinessKey])
      response.status(200).json({ status: 'ready' })
    } catch {
      recordFailure('readiness')
      response.status(503).json({ status: 'not ready' })
    }
  })

  const authConfig = loadAuthConfigFromEnv()
  const { adapter, headerPropagation } = createCloudAdapter(agent, authConfig)
  app.post(
    '/api/messages',
    authorizeJWT(authConfig),
    requireTrustedIssuer(authConfig.issuers ?? [], usesNativeIssuerValidation(authConfig)),
    requireWebChatServiceUrl,
    async (request, response, next) => {
      try {
        await adapter.process(request, response, (context) => agent.run(context), headerPropagation)
      } catch (error) {
        next(error)
      }
    }
  )

  app.use((error: unknown, _request: Request, response: Response, next: express.NextFunction) => {
    const status = httpErrorStatus(error)
    const category = status < 500 ? 'payload' : 'http'
    recordFailure(category)
    if (response.headersSent) {
      next(error)
      return
    }
    response.status(status).json({ error: 'Request could not be processed.' })
  })
  return app
}

function httpErrorStatus (error: unknown): number {
  if (typeof error !== 'object' || error === null || !('status' in error)) return 500
  const status = Number(error.status)
  return status >= 400 && status < 500 ? status : 500
}

function requireTrustedIssuer (trustedIssuers: readonly string[], nativeValidationEnabled: boolean): express.RequestHandler {
  return (request, _response, next) => {
    if (process.env.NODE_ENV !== 'production' || nativeValidationEnabled) {
      next()
      return
    }
    const identity = (request as Request & { user?: { iss?: unknown } }).user
    if (isTrustedIssuer(identity?.iss, trustedIssuers)) {
      next()
      return
    }
    next(Object.assign(new Error('Unauthorized issuer.'), { status: 401 }))
  }
}

export function usesNativeIssuerValidation (authConfig: AuthConfiguration): boolean {
  return (authConfig as AuthConfiguration & { validateIssuer?: unknown }).validateIssuer === true
}

export function isTrustedIssuer (issuer: unknown, trustedIssuers: readonly string[]): boolean {
  if (typeof issuer !== 'string') return false
  const candidate = issuer.toLowerCase()
  return trustedIssuers.some((trustedIssuer) => trustedIssuer.toLowerCase() === candidate)
}

function requireWebChatServiceUrl (request: Request, _response: Response, next: express.NextFunction): void {
  if (isAllowedActivityServiceUrl(request.body?.serviceUrl, process.env.NODE_ENV === 'production')) {
    next()
    return
  }
  next(Object.assign(new Error('Unsupported channel.'), { status: 400 }))
}

export function isAllowedActivityServiceUrl (serviceUrl: unknown, isProduction: boolean): boolean {
  if (isWebChatServiceUrl(serviceUrl)) return true
  if (isProduction) return false
  try {
    const host = new URL(String(serviceUrl ?? '')).hostname.toLowerCase()
    return host === 'localhost' || host === '127.0.0.1' || host === '[::1]'
  } catch {
    return false
  }
}

export function isWebChatServiceUrl (serviceUrl: unknown): boolean {
  try {
    return new URL(String(serviceUrl ?? '')).hostname.toLowerCase() === 'webchat.botframework.com'
  } catch {
    return false
  }
}

export function listen (app: Express, port: number): Server {
  const server = app.listen(port, () => {
    const address = server.address()
    const boundPort = typeof address === 'object' && address ? address.port : port
    const appServiceHostname = process.env.WEBSITE_HOSTNAME?.trim()
    const baseUrl = appServiceHostname
      ? `https://${appServiceHostname}`
      : `http://localhost:${boundPort}`
    console.log([
      'Agent server listening:',
      `- Messages: ${baseUrl}/api/messages`,
      `- Liveness: ${baseUrl}/health/live`,
      `- Readiness: ${baseUrl}/health/ready`
    ].join('\n'))
  })
  return server
}

export function closeServer (server: Server): Promise<void> {
  return new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { Attachment } from '@microsoft/agents-activity'
import { CardFactory, TurnContext } from '@microsoft/agents-hosting'

interface CardFact {
  title: string;
  value: string;
}

export function createCallerCard (context: TurnContext): Attachment {
  const claims = context.identity as Record<string, unknown> | undefined
  const recipient = context.activity.recipient
  const authenticated = Boolean(claims && Object.keys(claims).length > 0)
  const facts: CardFact[] = [
    fact('Claims available', authenticated ? 'Yes' : 'No'),
    fact('Authenticated', authenticated ? 'Yes' : 'No'),
    fact('Authentication type', authenticated ? 'JWT bearer' : undefined),
    fact('Issuer', claim(claims, 'iss')),
    fact('Audience', claim(claims, 'aud')),
    fact('Tenant ID', claim(claims, 'tid', 'http://schemas.microsoft.com/identity/claims/tenantid')),
    fact('Subject', claim(claims, 'sub', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier')),
    fact('Caller app ID', claim(claims, 'azp', 'appid')),
    fact('Token version', claim(claims, 'ver')),
    fact('Issued', epochClaim(claims, 'iat')),
    fact('Expires', epochClaim(claims, 'exp')),
    fact('Recipient role', recipient?.role),
    fact('Agent identity', recipient?.agenticAppId),
    fact('Agent user', recipient?.agenticUserId),
    fact('Activity tenant', recipient?.tenantId)
  ]

  return CardFactory.adaptiveCard({
    type: 'AdaptiveCard',
    version: '1.5',
    body: [
      {
        type: 'TextBlock',
        size: 'Medium',
        weight: 'Bolder',
        text: 'Inbound agentic caller'
      },
      {
        type: 'TextBlock',
        wrap: true,
        text: 'Selected token claims and activity routing values. The raw token is never displayed.'
      },
      {
        type: 'FactSet',
        facts
      }
    ]
  })
}

function fact (title: string, value: unknown): CardFact {
  const text = Array.isArray(value) ? value.join(', ') : String(value ?? '').trim()
  return { title, value: text || 'Unavailable' }
}

function claim (claims: Record<string, unknown> | undefined, ...names: string[]): unknown {
  for (const name of names) {
    if (claims?.[name] !== undefined) {
      return claims[name]
    }
  }

  return undefined
}

function epochClaim (claims: Record<string, unknown> | undefined, name: string): string | undefined {
  const value = claim(claims, name)
  const seconds = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(seconds) ? new Date(seconds * 1000).toISOString() : undefined
}

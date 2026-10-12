// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

export type Impact = 'low' | 'medium' | 'high'
export type Stage = 'summary' | 'impact' | 'complete'

export interface SupportIssueCaptureState {
  version: 1
  stage: Stage
  summary?: string
  impact?: Impact
}

export interface IssueCaptureResult {
  readonly state: SupportIssueCaptureState
  readonly reply: string
}

export function initialIssueCaptureState (): SupportIssueCaptureState {
  return { version: 1, stage: 'summary' }
}

export function advanceIssueCapture (current: SupportIssueCaptureState, message: string): IssueCaptureResult {
  const value = message.trim()
  if (!value) return { state: current, reply: 'Please provide the requested information.' }

  switch (current.stage) {
    case 'summary':
      return {
        state: { ...current, summary: value, stage: 'impact' },
        reply: 'What is the impact: low, medium, or high?',
      }
    case 'impact': {
      const impact = parseImpact(value)
      if (!impact) return { state: current, reply: 'Reply with low, medium, or high.' }
      return {
        state: { ...current, impact, stage: 'complete' },
        reply: `Support issue capture saved to this conversation with ${impact} impact.`,
      }
    }
    case 'complete':
      return { state: current, reply: 'This conversation already has a completed support issue capture.' }
  }
}

export function parseImpact (value: string): Impact | undefined {
  const normalized = value.trim().toLowerCase()
  return normalized === 'low' || normalized === 'medium' || normalized === 'high' ? normalized : undefined
}

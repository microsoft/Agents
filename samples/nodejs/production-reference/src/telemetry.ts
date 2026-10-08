// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { metrics, SpanStatusCode, trace } from '@opentelemetry/api'
import { shutdownAzureMonitor, useAzureMonitor } from '@azure/monitor-opentelemetry'

const telemetryEnabled = process.env.NODE_ENV === 'production' && Boolean(process.env.APPLICATIONINSIGHTS_CONNECTION_STRING)

if (telemetryEnabled) {
  useAzureMonitor({
    enableLiveMetrics: false,
    tracesPerSecond: 5,
  })
}

const tracer = trace.getTracer('agents-sdk-production-reference')
const meter = metrics.getMeter('agents-sdk-production-reference')
const turnCounter = meter.createCounter('agent.turns.total')
const failureCounter = meter.createCounter('agent.failures.total')

export function recordTurn (): void {
  turnCounter.add(1)
}

export function recordFailure (category: 'turn' | 'readiness' | 'payload' | 'http'): void {
  failureCounter.add(1, { category })
}

export function runIssueCaptureTurn<T> (action: () => Promise<T>): Promise<T> {
  return tracer.startActiveSpan('agent.support_issue_capture.turn', async (span) => {
    try {
      return await action()
    } catch (error) {
      span.setStatus({ code: SpanStatusCode.ERROR })
      throw error
    } finally {
      span.end()
    }
  })
}

export async function shutdownTelemetry (): Promise<void> {
  if (telemetryEnabled) await shutdownAzureMonitor()
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { metrics, trace } from '@opentelemetry/api'
import { logs, SeverityNumber } from '@opentelemetry/api-logs'

export class AgentTelemetry {
  public static serviceName = 'OTelAgent'
  public static serviceVersion = '1.0.0'

  public static tracer = trace.getTracer(this.serviceName, this.serviceVersion)
  private static meter = metrics.getMeter(this.serviceName, this.serviceVersion)
  private static logger = logs.getLogger(this.serviceName)

  public static routeExecutedCounter = this.meter.createCounter('agent.routes.executed.count', {
    unit: 'routes',
    description: 'Number of routes executed by the agent'
  })

  public static messageProcessingDuration = this.meter.createHistogram('agent.message.processing.duration', {
    unit: 'ms',
    description: 'Duration of message processing in milliseconds'
  })

  public static logInfo (body: string, attributes: Record<string, string | number>): void {
    this.logger.emit({
      severityNumber: SeverityNumber.INFO,
      severityText: 'INFO',
      body,
      attributes
    })
  }

  public static logError (body: string, attributes: Record<string, string | number>): void {
    this.logger.emit({
      severityNumber: SeverityNumber.ERROR,
      severityText: 'ERROR',
      body,
      attributes
    })
  }
}

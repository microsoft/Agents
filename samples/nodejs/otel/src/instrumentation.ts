// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { NodeSDK } from '@opentelemetry/sdk-node'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-grpc'
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-grpc'
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-grpc'
import {
  PeriodicExportingMetricReader,
  PushMetricExporter,
} from '@opentelemetry/sdk-metrics'
import {
  BatchLogRecordProcessor,
  LogRecordExporter,
} from '@opentelemetry/sdk-logs'
import { HttpInstrumentation } from '@opentelemetry/instrumentation-http'
import { UndiciInstrumentation } from '@opentelemetry/instrumentation-undici'
import { resourceFromAttributes } from '@opentelemetry/resources'
import {
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION,
} from '@opentelemetry/semantic-conventions'
import { AlwaysOnSampler, ParentBasedSampler, Sampler, SamplingDecision, SpanExporter } from '@opentelemetry/sdk-trace-base'
import { hostname } from 'os'
import { SpanNames } from '@microsoft/agents-telemetry'

const traceExporter: SpanExporter = new OTLPTraceExporter()
const metricExporter: PushMetricExporter = new OTLPMetricExporter()
const logExporter: LogRecordExporter = new OTLPLogExporter()

// Example of a custom sampler that filters out spans based on their name. This is useful for disabling telemetry for certain categories of spans, such as storage operations.
class SpanNameFilteringSampler implements Sampler {
  private readonly filteredSpanNames: ReadonlySet<string>
  private readonly parentBased = new ParentBasedSampler({
    root: new AlwaysOnSampler(),
  })

  constructor (spanNames: Iterable<string>) {
    this.filteredSpanNames = new Set(spanNames)
  }

  shouldSample: Sampler['shouldSample'] = (context, traceId, spanName, spanKind, attributes, links) => {
    return this.filteredSpanNames.has(spanName)
      ? { decision: SamplingDecision.NOT_RECORD }
      : this.parentBased.shouldSample(context, traceId, spanName, spanKind, attributes, links)
  }

  toString () {
    return `SpanNameFilteringSampler(${[...this.filteredSpanNames].join(',')})`
  }
}

// configure the SDK to export telemetry data.
const sdk = new NodeSDK({
  sampler: new SpanNameFilteringSampler([
    SpanNames.STORAGE_READ, SpanNames.STORAGE_WRITE, SpanNames.STORAGE_DELETE
  ]),
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: 'OTelAgent',
    [ATTR_SERVICE_VERSION]: '1.0.0',
    'service.instance.id': hostname() ?? 'unknown',
    'telemetry.sdk.language': 'nodejs'
  }),
  traceExporter,
  metricReader: new PeriodicExportingMetricReader({ exporter: metricExporter }),
  logRecordProcessors: [
    new BatchLogRecordProcessor({ exporter: logExporter }),
  ],
  instrumentations: [
    new HttpInstrumentation(),
    new UndiciInstrumentation()
  ]
})

sdk.start()

let isShuttingDown = false

const shutdownHandler = (signal: NodeJS.Signals) => {
  if (isShuttingDown) {
    return
  }
  isShuttingDown = true

  const shutdownTimeout = setTimeout(() => {
    console.error(`OTel SDK shutdown timed out after receiving ${signal}`)
    process.exit(1)
  }, 5000)
  shutdownTimeout.unref()

  sdk.shutdown()
    .then(() => {
      console.log('OTel SDK shut down successfully')
      clearTimeout(shutdownTimeout)
      process.exit(0)
    })
    .catch((error) => {
      console.error('Error shutting down OTel SDK', error)
      clearTimeout(shutdownTimeout)
      process.exit(1)
    })
}

process.once('SIGTERM', () => shutdownHandler('SIGTERM'))
process.once('SIGINT', () => shutdownHandler('SIGINT'))

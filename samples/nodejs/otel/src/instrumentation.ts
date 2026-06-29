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
import { resourceFromAttributes } from '@opentelemetry/resources'
import {
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION,
} from '@opentelemetry/semantic-conventions'
import { SpanExporter } from '@opentelemetry/sdk-trace-base'
import { hostname } from 'os'

const traceExporter: SpanExporter = new OTLPTraceExporter()
const metricExporter: PushMetricExporter = new OTLPMetricExporter()
const logExporter: LogRecordExporter = new OTLPLogExporter()

// configure the SDK to export telemetry data.
const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: 'OTelAgent',
    [ATTR_SERVICE_VERSION]: '1.0.0',
    'service.instance.id': hostname() ?? 'unknown',
    'telemetry.sdk.language': 'nodejs'
  }),
  traceExporter,
  metricReader: new PeriodicExportingMetricReader({ exporter: metricExporter }),
  logRecordProcessors: [
    new BatchLogRecordProcessor(logExporter),
  ],
  instrumentations: [
    new HttpInstrumentation()
  ]
})

sdk.start()

const shutdownHandler = () => {
  sdk.shutdown()
    .then(() => console.log('OTel SDK shut down successfully'))
    .catch((error) => {
      console.error('Error shutting down OTel SDK', error)
      process.exitCode = 1
    })
}

process.on('SIGTERM', shutdownHandler)
process.on('SIGINT', shutdownHandler)

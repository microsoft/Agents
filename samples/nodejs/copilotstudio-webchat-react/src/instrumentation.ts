// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import 'zone.js'
import { context, propagation, trace } from '@opentelemetry/api'
import { ZoneContextManager } from '@opentelemetry/context-zone'
import { CompositePropagator, W3CBaggagePropagator, W3CTraceContextPropagator } from '@opentelemetry/core'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto'
import { registerInstrumentations } from '@opentelemetry/instrumentation'
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load'
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { BatchSpanProcessor, TracerProvider } from '@opentelemetry/sdk-trace'
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions'

const provider = new TracerProvider({
  resource: resourceFromAttributes({ [ATTR_SERVICE_NAME]: 'copilotstudio-webchat-react' }),
  spanProcessors: [
    new BatchSpanProcessor({ exporter: new OTLPTraceExporter({ url: 'http://localhost:4318/v1/traces' }) })
  ]
})

trace.setGlobalTracerProvider(provider)
context.setGlobalContextManager(new ZoneContextManager().enable())
propagation.setGlobalPropagator(new CompositePropagator({
  propagators: [new W3CTraceContextPropagator(), new W3CBaggagePropagator()]
}))
registerInstrumentations({
  instrumentations: [
    new DocumentLoadInstrumentation(),
    new FetchInstrumentation({ ignoreUrls: [/\/v1\/traces$/] })
  ]
})

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import 'zone.js'
import { ZoneContextManager } from '@opentelemetry/context-zone'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto'
import { registerInstrumentations } from '@opentelemetry/instrumentation'
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load'
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base'
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web'
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions'

const provider = new WebTracerProvider({
  resource: resourceFromAttributes({ [ATTR_SERVICE_NAME]: 'copilotstudio-webchat-react' }),
  spanProcessors: [new BatchSpanProcessor(new OTLPTraceExporter({ url: 'http://localhost:4318/v1/traces' }))]
})

provider.register({ contextManager: new ZoneContextManager() })
registerInstrumentations({
  instrumentations: [
    new DocumentLoadInstrumentation(),
    new FetchInstrumentation({ ignoreUrls: [/\/v1\/traces$/] })
  ]
})

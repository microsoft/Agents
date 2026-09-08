// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { createBuilder, OtlpProtocol } from './.aspire/modules/aspire.mjs'

const builder = await createBuilder()

await builder
  .addExecutable('webchat', 'npm', '..', ['run', 'start'])
  .withHttpEndpoint({ port: 3000, targetPort: 3000, isProxied: false })
  .withOtlpExporter({ protocol: OtlpProtocol.HttpProtobuf })

await builder.build().run()

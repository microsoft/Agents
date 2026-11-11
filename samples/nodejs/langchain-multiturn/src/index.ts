// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { weatherAgent } from './weatherAgent.js'

startServer(weatherAgent)

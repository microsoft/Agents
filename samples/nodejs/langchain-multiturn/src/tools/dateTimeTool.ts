// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { tool } from '@langchain/core/tools'
import { z } from 'zod'

const noInputSchema = z.object({}).strict()

const dateTool = tool(
  async () => new Date().toLocaleDateString(undefined, { dateStyle: 'full' }),
  { name: 'date', description: 'Get the current date.', schema: noInputSchema }
)

const todayTool = tool(
  async () => new Date().toLocaleDateString(undefined, { dateStyle: 'full' }),
  { name: 'today', description: 'Get the current date.', schema: noInputSchema }
)

const nowTool = tool(
  async () => new Date().toLocaleString(undefined, { dateStyle: 'full', timeStyle: 'short' }),
  { name: 'now', description: 'Get the current date and time in the local time zone.', schema: noInputSchema }
)

export const dateTimeTools = [dateTool, todayTool, nowTool]

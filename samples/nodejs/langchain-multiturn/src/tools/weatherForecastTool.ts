// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { tool } from '@langchain/core/tools'
import { z } from 'zod'
import { reportProgress } from './progressContext.js'

const weatherForecastInputSchema = z.object({
  date: z.string().describe('The date for the forecast, for example 2026-09-19'),
  location: z.string().describe('The location for the forecast, for example Seattle, WA')
}).strict()

function displayDate (date: string): string {
  const parsedDate = /^\d{4}-\d{2}-\d{2}$/.test(date)
    ? new Date(`${date}T00:00:00`)
    : new Date(date)
  return Number.isNaN(parsedDate.valueOf())
    ? date
    : parsedDate.toLocaleDateString(undefined, { dateStyle: 'long' })
}

export const weatherForecastTool = tool(
  async ({ date, location }) => {
    await reportProgress(`Looking up the weather in ${location} for ${displayDate(date)}`)

    const temperatureC = Math.floor(Math.random() * 75) - 20
    const temperatureF = Math.round((temperatureC * 9 / 5) + 32)
    return { date, location, temperatureC, temperatureF }
  },
  {
    name: 'get_forecast_for_date',
    description: 'Get a synthetic weather forecast for a specific date and location.',
    schema: weatherForecastInputSchema
  }
)

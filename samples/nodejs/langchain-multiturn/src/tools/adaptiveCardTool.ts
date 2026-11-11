// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { tool } from '@langchain/core/tools'
import { z } from 'zod'

const adaptiveCardInputSchema = z.object({
  location: z.string(),
  date: z.string(),
  temperatureC: z.number().int(),
  temperatureF: z.number().int()
}).strict()

export const adaptiveCardTool = tool(
  async ({ location, date, temperatureC, temperatureF }) => ({
    type: 'AdaptiveCard',
    version: '1.5',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    body: [
      { type: 'TextBlock', text: `Weather forecast for ${location}`, weight: 'Bolder', size: 'Medium', wrap: true },
      {
        type: 'FactSet',
        facts: [
          { title: 'Date', value: date },
          { title: 'Temperature', value: `${temperatureC} C / ${temperatureF} F` }
        ]
      }
    ],
    actions: [
      {
        type: 'Action.OpenUrl',
        title: 'More details',
        url: `https://www.msn.com/en-us/weather/forecast/in-${encodeURIComponent(location)}`
      }
    ]
  }),
  {
    name: 'get_adaptive_card_for_data',
    description: 'Create an Adaptive Card 1.5 for weather forecast data.',
    schema: adaptiveCardInputSchema
  }
)

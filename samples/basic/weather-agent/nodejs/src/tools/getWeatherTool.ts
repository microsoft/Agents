import { tool } from '@langchain/core/tools'
import { z } from 'zod'

const getWeatherSchema = z.object({
  date: z.string(),
  location: z.string(),
  temperatureC: z.number().optional()
})

export const GetWeatherTool = tool(
  async ({ date, location }) => {
    console.log('************Getting random weather for', date, location)
    const min = -22
    const max = 55
    return {
      date,
      location,
      temperatureC: Math.floor(Math.random() * (max - min + 1)) + min
    }
  },
  {
    name: 'GetWeather',
    description: 'Retrieve the weather forecast for a specific date. This is a placeholder for a real implementation and currently only returns a random temperature. This would typically call a weather service API.',
    schema: getWeatherSchema,
  }
)

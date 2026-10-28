// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { Activity, ActivityTypes } from '@microsoft/agents-activity'
import { AgentApplicationBuilder, TurnContext } from '@microsoft/agents-hosting'
import { AzureChatOpenAI, ChatOpenAI } from '@langchain/openai'
import { MemorySaver, REMOVE_ALL_MESSAGES } from '@langchain/langgraph'
import { HumanMessage, RemoveMessage } from '@langchain/core/messages'
import { createAgent, createMiddleware, providerStrategy } from 'langchain'
import { z } from 'zod'
import { adaptiveCardTool } from './tools/adaptiveCardTool.js'
import { dateTimeTools } from './tools/dateTimeTool.js'
import { weatherForecastTool } from './tools/weatherForecastTool.js'
import { runWithTurnContext } from './tools/progressContext.js'

const welcomeMessage = 'Hello and welcome! I\'m here to help with all your weather forecast needs!'
const processingMessage = 'Working on a response for you'
const failureMessage = 'Sorry, I couldn\'t get the weather forecast at the moment.'
const maximumHistoryMessages = 20

const agentInstructions = `
You are a friendly assistant that helps people find a weather forecast for a given time and place.
Ask follow-up questions until you have both a location and a date. Once you have a forecast, use the
weather forecast tool and adaptive card tool, then return the result as an Adaptive Card.

The Adaptive Card must use version 1.5 and include the location, date, temperature in Celsius and
Fahrenheit, and a button for more details. The button must point to
https://www.msn.com/en-us/weather/forecast/in-{location}, replacing {location} with a URL-encoded location.

Return Text while asking follow-up questions and AdaptiveCard after obtaining a forecast.
`

const textResponseSchema = z.object({
  contentType: z.literal('Text'),
  content: z.string()
}).strict()

const factSchema = z.object({
  title: z.string(),
  value: z.string()
}).strict()

const textBlockSchema = z.object({
  type: z.literal('TextBlock'),
  text: z.string(),
  weight: z.literal('Bolder'),
  size: z.literal('Medium'),
  wrap: z.boolean()
}).strict()

const factSetSchema = z.object({
  type: z.literal('FactSet'),
  facts: z.array(factSchema)
}).strict()

const openUrlActionSchema = z.object({
  type: z.literal('Action.OpenUrl'),
  title: z.string(),
  url: z.string()
}).strict()

const adaptiveCardResponseSchema = z.object({
  contentType: z.literal('AdaptiveCard'),
  content: z.object({
    type: z.literal('AdaptiveCard'),
    version: z.literal('1.5'),
    $schema: z.literal('http://adaptivecards.io/schemas/adaptive-card.json'),
    body: z.array(z.union([textBlockSchema, factSetSchema])).min(2).max(2),
    actions: z.array(openUrlActionSchema).min(1).max(1)
  }).strict()
}).strict()

const weatherForecastAgentResponseSchema = z.discriminatedUnion('contentType', [
  textResponseSchema,
  adaptiveCardResponseSchema
])

type WeatherForecastAgentResponse = z.infer<typeof weatherForecastAgentResponseSchema>

function isWeatherAdaptiveCard (card: unknown): boolean {
  if (typeof card !== 'object' || card === null) {
    return false
  }

  const value = card as Record<string, unknown>
  if (
    value.type !== 'AdaptiveCard' ||
    value.version !== '1.5' ||
    value.$schema !== 'http://adaptivecards.io/schemas/adaptive-card.json' ||
    !Array.isArray(value.body) ||
    !Array.isArray(value.actions)
  ) {
    return false
  }

  const body = value.body.filter((item): item is Record<string, unknown> =>
    typeof item === 'object' && item !== null
  )
  const heading = body.find(item => item.type === 'TextBlock')
  const factSet = body.find(item => item.type === 'FactSet')
  const facts = Array.isArray(factSet?.facts) ? factSet.facts : []
  const hasFact = (title: string) => facts.some(fact => {
    if (typeof fact !== 'object' || fact === null) {
      return false
    }

    const weatherFact = fact as Record<string, unknown>
    return weatherFact.title === title &&
      typeof weatherFact.value === 'string' &&
      weatherFact.value.trim().length > 0
  })

  const hasWeatherHeading = typeof heading?.text === 'string' &&
    heading.text.startsWith('Weather forecast for ') &&
    heading.text.slice('Weather forecast for '.length).trim().length > 0
  const hasMsnAction = value.actions.some(action => {
    if (typeof action !== 'object' || action === null) {
      return false
    }

    const cardAction = action as Record<string, unknown>
    return cardAction.type === 'Action.OpenUrl' &&
      typeof cardAction.url === 'string' &&
      cardAction.url.startsWith('https://www.msn.com/en-us/weather/forecast/in-')
  })

  return hasWeatherHeading && hasFact('Date') && hasFact('Temperature') && hasMsnAction
}

// Azure OpenAI strict structured output requires an object at the root of the JSON
// schema. The discriminated union above is retained for local validation, but it
// serializes to a root-level anyOf and is therefore not suitable for the request.
const weatherForecastAgentTransportSchema = z.object({
  contentType: z.enum(['Text', 'AdaptiveCard']),
  content: z.union([
    z.string(),
    adaptiveCardResponseSchema.shape.content
  ])
}).strict()

function requiredSetting (name: string): string {
  const value = process.env[name]?.trim()
  if (!value) {
    throw new Error(`${name} environment variable is missing and required.`)
  }
  return value
}

const useAzureOpenAI = (process.env.USE_AZURE_OPENAI ?? process.env.USE_AZURE_OPENAI_API) === 'true'

const agentModel = useAzureOpenAI
  ? new AzureChatOpenAI({
    azureOpenAIApiKey: requiredSetting('AZURE_OPENAI_API_KEY'),
    azureOpenAIApiInstanceName: requiredSetting('AZURE_OPENAI_API_INSTANCE_NAME'),
    azureOpenAIApiDeploymentName: requiredSetting('AZURE_OPENAI_DEPLOYMENT_NAME'),
    azureOpenAIApiVersion: requiredSetting('AZURE_OPENAI_API_VERSION'),
    temperature: 0,
    topP: 1
  })
  : new ChatOpenAI({
    apiKey: requiredSetting('OPENAI_API_KEY'),
    model: requiredSetting('OPENAI_MODEL_ID'),
    temperature: 0,
    topP: 1
  })

const boundedHistoryMiddleware = createMiddleware({
  name: 'BoundedHistory',
  beforeModel: (state) => {
    if (state.messages.length <= maximumHistoryMessages) {
      return
    }

    return {
      messages: [
        new RemoveMessage({ id: REMOVE_ALL_MESSAGES }),
        ...state.messages.slice(-maximumHistoryMessages)
      ]
    }
  }
})

const weatherForecastAgent = createAgent({
  model: agentModel,
  name: 'WeatherForecastAgent',
  systemPrompt: agentInstructions,
  tools: [weatherForecastTool, adaptiveCardTool, ...dateTimeTools],
  middleware: [boundedHistoryMiddleware],
  checkpointer: new MemorySaver(),
  responseFormat: providerStrategy({
    schema: weatherForecastAgentTransportSchema,
    strict: true
  })
})

async function invokeWeatherForecastAgent (
  input: string,
  conversationId: string
): Promise<WeatherForecastAgentResponse> {
  let lastError: unknown
  for (let attempt = 0; attempt < 2; attempt++) {
    const message = attempt === 0
      ? input
      : 'The previous response did not match the required schema. Return only a valid response object.'

    try {
      const result = await weatherForecastAgent.invoke(
        { messages: [new HumanMessage(message)] },
        { configurable: { thread_id: conversationId } }
      )
      const response = weatherForecastAgentResponseSchema.parse(result.structuredResponse)
      if (response.contentType === 'AdaptiveCard' && !isWeatherAdaptiveCard(response.content)) {
        throw new Error('The agent response did not contain the required weather Adaptive Card content.')
      }
      return response
    } catch (error) {
      lastError = error
    }
  }

  throw lastError
}

export const weatherAgent = new AgentApplicationBuilder().build()

weatherAgent.onConversationUpdate('membersAdded', async (context) => {
  if (context.activity.membersAdded?.some(member => member.id !== context.activity.recipient?.id)) {
    await context.sendActivity(welcomeMessage)
  }
})

weatherAgent.onActivity(ActivityTypes.Message, async (context: TurnContext) => {
  context.streamingResponse.setFeedbackLoop(true)
  context.streamingResponse.setGeneratedByAILabel(true)
  context.streamingResponse.queueInformativeUpdate(processingMessage)

  try {
    const userText = context.activity.text?.trim()
    if (!userText) {
      context.streamingResponse.queueTextChunk('Please enter a weather question.')
      return
    }

    const response = await runWithTurnContext(context, () => invokeWeatherForecastAgent(
      userText,
      context.activity.conversation!.id
    ))
    if (response.contentType === 'Text') {
      context.streamingResponse.queueTextChunk(response.content)
    } else {
      context.streamingResponse.setFinalMessage(Activity.fromObject({ type: ActivityTypes.Message }))
      context.streamingResponse.addAttachment({
        contentType: 'application/vnd.microsoft.card.adaptive',
        content: response.content
      })
    }
  } catch (error) {
    console.error('Error during agent execution:', error)
    context.streamingResponse.queueTextChunk(failureMessage)
  } finally {
    await context.streamingResponse.endStream()
  }
})

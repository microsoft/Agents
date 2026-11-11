// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { createAzure } from '@ai-sdk/azure'
import { Activity, ActivityTypes } from '@microsoft/agents-activity'
import { AgentApplication, TurnContext, TurnState } from '@microsoft/agents-hosting'
import { startServer } from '@microsoft/agents-hosting-express'
import { streamText } from 'ai'

const requiredEnvironmentVariable = (name: string): string => {
  const value = process.env[name]
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`)
  }

  return value
}

const azureOpenAIEndpoint = requiredEnvironmentVariable('AZURE_OPENAI_ENDPOINT').replace(/\/+$/, '')
const azureOpenAIHostname = new URL(azureOpenAIEndpoint).hostname
const azureOpenAIBaseURL = azureOpenAIHostname.endsWith('.services.ai.azure.com')
  ? `${azureOpenAIEndpoint}/openai/v1`
  : `${azureOpenAIEndpoint}/openai`
const azureOpenAIProvider = createAzure({
  baseURL: azureOpenAIBaseURL,
  apiKey: requiredEnvironmentVariable('AZURE_OPENAI_API_KEY')
})
const deploymentName = requiredEnvironmentVariable('AZURE_OPENAI_DEPLOYMENT_NAME')
const agent = new AgentApplication<TurnState>()

agent.onConversationUpdate('membersAdded', async (context: TurnContext) => {
  const recipientId = context.activity.recipient?.id
  const userJoined = context.activity.membersAdded?.some((member) => member.id !== recipientId)

  if (userJoined) {
    await context.sendActivity("Say anything and I'll recite poetry.")
  }
})

agent.addRoute(
  async (context: TurnContext) => {
    const value = context.activity.value
    return context.activity.type === ActivityTypes.Invoke &&
      context.activity.name === 'message/submitAction' &&
      typeof value === 'object' &&
      value !== null &&
      'actionName' in value &&
      value.actionName === 'feedback'
  },
  async (context: TurnContext) => {
    const value = context.activity.value as { actionValue?: unknown }
    console.log('Feedback received:', JSON.stringify(value.actionValue ?? value))

    await context.sendActivity(Activity.fromObject({
      type: ActivityTypes.InvokeResponse,
      value: { status: 200 }
    }))
    await context.sendActivity('Thanks for submitting your feedback.')
  },
  true
)

agent.onActivity(ActivityTypes.Message, async (context: TurnContext) => {
  context.streamingResponse.setFeedbackLoop(true)
  context.streamingResponse.setGeneratedByAILabel(true)
  context.streamingResponse.setSensitivityLabel({
    type: 'https://schema.org/Message',
    '@type': 'CreativeWork',
    name: 'Internal'
  })

  await context.streamingResponse.queueInformativeUpdate('Hold on for an awesome poem about Apollo...')

  try {
    const { fullStream } = streamText({
      model: azureOpenAIProvider(deploymentName),
      system: `You are a creative assistant who has deeply studied Greek and Roman gods and the Percy Jackson series.
You write poems about the Greek gods as they are depicted in the Percy Jackson books.
You format the poems in a way that is easy to read and understand.
You break your poems into stanzas.
You format your poems in Markdown using blank lines to separate stanzas.`,
      prompt: 'Write a poem of about 500 words about the Greek god Apollo as depicted in the Percy Jackson books.'
    })

    for await (const part of fullStream) {
      if (part.type === 'text-delta' && part.text.length > 0) {
        await context.streamingResponse.queueTextChunk(part.text)
      } else if (part.type === 'error') {
        throw part.error instanceof Error ? part.error : new Error(String(part.error))
      }
    }
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      console.info('Streaming was cancelled.')
    } else {
      console.error('Error during streaming:', error)
      await context.streamingResponse.queueTextChunk('An error occurred while generating the poem. Please try again later.')
    }
  } finally {
    await context.streamingResponse.endStream()
  }
})

startServer(agent, {
  beforeListen: (app) => {
    app.get('/', (_request: unknown, response: { send: (body: string) => unknown }) => response.send('Azure AI Streaming Sample'))
  }
})

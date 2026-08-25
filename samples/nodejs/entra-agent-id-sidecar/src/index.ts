// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { ActivityTypes } from '@microsoft/agents-activity'
import {
  AgentApplication,
  CloudAdapter,
  ConnectionManager,
  loadAuthConfigFromEnv,
  MemoryStorage,
  MessageFactory,
  TurnContext,
  TurnState
} from '@microsoft/agents-hosting'
import { startServer } from '@microsoft/agents-hosting-express'
import { createCallerCard } from './callerCard.js'

interface ConversationState {
  count: number;
}

type ApplicationTurnState = TurnState<ConversationState>

const usage = 'Send `caller` to display inbound agentic token details. Other messages are echoed.'
const authConfiguration = loadAuthConfigFromEnv()
const connections = new ConnectionManager(
  undefined,
  authConfiguration.connections,
  authConfiguration.connectionsMap,
  authConfiguration
)
const adapter = new CloudAdapter(authConfiguration)

const agentApp = new AgentApplication<ApplicationTurnState>({
  storage: new MemoryStorage(),
  adapter,
  connections
})

agentApp.onConversationUpdate(
  'membersAdded',
  async (context: TurnContext) => {
    await context.sendActivity(`Welcome! ${usage}`)
  }
)

agentApp.onActivity(
  ActivityTypes.Message,
  async (context: TurnContext) => {
    const command = context.activity.text?.trim().toLowerCase()
    if (command === 'caller') {
      await context.sendActivity(MessageFactory.attachment(createCallerCard(context)))
      return
    }

    if (command === 'help') {
      await context.sendActivity(usage)
      return
    }

    await context.sendActivity(`Echo: ${context.activity.text ?? ''}`)
  }
)

startServer(agentApp, { authConfig: authConfiguration })

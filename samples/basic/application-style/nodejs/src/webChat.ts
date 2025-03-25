// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { TurnState, MemoryStorage, TurnContext, AgentApplication, AttachmentDownloader }
  from '@microsoft/agents-bot-hosting'
import { version } from '@microsoft/agents-bot-hosting/package.json'
import { ActivityTypes } from '@microsoft/agents-bot-activity'

interface ConversationState {
  count: number;
}
type ApplicationTurnState = TurnState<ConversationState>

const downloader = new AttachmentDownloader()

// Define storage and application
const storage = new MemoryStorage()
export const webChatAgent = new AgentApplication<ApplicationTurnState>({
  storage,
  fileDownloaders: [downloader]
})

// Listen for user to say '/reset' and then delete conversation state
webChatAgent.message('/reset', async (context: TurnContext, state: ApplicationTurnState) => {
  state.deleteConversationState()
  await context.sendActivity('Ok I\'ve deleted the current conversation state.')
})

webChatAgent.message('/count', async (context: TurnContext, state: ApplicationTurnState) => {
  const count = state.conversation.count ?? 0
  await context.sendActivity(`The count is ${count}`)
})

webChatAgent.message('/diag', async (context: TurnContext, state: ApplicationTurnState) => {
  await state.load(context, storage)
  await context.sendActivity(JSON.stringify(context.activity))
})

webChatAgent.message('/state', async (context: TurnContext, state: ApplicationTurnState) => {
  await state.load(context, storage)
  await context.sendActivity(JSON.stringify(state))
})

webChatAgent.message('/runtime', async (context: TurnContext, state: ApplicationTurnState) => {
  const runtime = {
    nodeversion: process.version,
    sdkversion: version
  }
  await context.sendActivity(JSON.stringify(runtime))
})

webChatAgent.conversationUpdate('membersAdded', async (context: TurnContext, state: ApplicationTurnState) => {
  await state.load(context, storage)
  await context.sendActivity('Welcome to the conversation!')
  await context.sendActivity(JSON.stringify(context.activity.membersAdded))
  await context.sendActivity(JSON.stringify(state))
})

// Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
webChatAgent.activity(ActivityTypes.Message, async (context: TurnContext, state: ApplicationTurnState) => {
  // Increment count state
  let count = state.conversation.count ?? 0
  state.conversation.count = ++count

  // Echo back users request
  await context.sendActivity(`[${count}] you said: ${context.activity.text}`)
})

webChatAgent.activity(/^message/, async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity(`Matched with regex: ${context.activity.type}`)
})

webChatAgent.activity(
  async (context: TurnContext) => Promise.resolve(context.activity.type === 'message'),
  async (context, state) => {
    await context.sendActivity(`Matched function: ${context.activity.type}`)
  }
)

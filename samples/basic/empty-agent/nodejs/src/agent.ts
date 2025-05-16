// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { TurnState, MemoryStorage, TurnContext, AgentApplication, AttachmentDownloader }
  from '@microsoft/agents-hosting'
import { version } from '@microsoft/agents-hosting/package.json'
import { ActivityTypes } from '@microsoft/agents-activity'

interface ConversationState {
  count: number;
}
type ApplicationTurnState = TurnState<ConversationState>

const downloader = new AttachmentDownloader()

// Define storage and application
const storage = new MemoryStorage()
export const agentApp = new AgentApplication<ApplicationTurnState>({
  storage,
  fileDownloaders: [downloader]
})

// Listen for user to say '/reset' and then delete conversation state
agentApp.onMessage('/reset', async (context: TurnContext, state: ApplicationTurnState) => {
  state.deleteConversationState()
  await context.sendActivity('Ok I\'ve deleted the current conversation state.')
})

agentApp.onMessage('/count', async (context: TurnContext, state: ApplicationTurnState) => {
  const count = state.conversation.count ?? 0
  await context.sendActivity(`The count is ${count}`)
})

agentApp.onMessage('/diag', async (context: TurnContext, state: ApplicationTurnState) => {
  await state.load(context, storage)
  await context.sendActivity(JSON.stringify(context.activity))
})

agentApp.onMessage('/state', async (context: TurnContext, state: ApplicationTurnState) => {
  await state.load(context, storage)
  await context.sendActivity(JSON.stringify(state))
})

agentApp.onMessage('/runtime', async (context: TurnContext, state: ApplicationTurnState) => {
  const runtime = {
    nodeversion: process.version,
    sdkversion: version
  }
  await context.sendActivity(JSON.stringify(runtime))
})

agentApp.onConversationUpdate('membersAdded', async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity('🚀 Echo bot running on Agents SDK version: ' + version)
})

// Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, state: ApplicationTurnState) => {
  // Increment count state
  let count = state.conversation.count ?? 0
  state.conversation.count = ++count

  // Echo back users request
  await context.sendActivity(`[${count}] you said: ${context.activity.text}`)
})

agentApp.onActivity(/^message/, async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity(`Matched with regex: ${context.activity.type}`)
})

agentApp.onActivity(
  async (context: TurnContext) => Promise.resolve(context.activity.type === 'message'),
  async (context, state) => {
    await context.sendActivity(`Matched function: ${context.activity.type}`)
  }
)

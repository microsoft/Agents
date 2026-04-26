// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplication, AttachmentDownloader, MemoryStorage, TurnContext, TurnState } from '@microsoft/agents-hosting';
import { startServer } from '@microsoft/agents-hosting-express';

const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms))

// Create custom conversation state properties.  This is
// used to store customer properties in conversation state.
interface ConversationState {
  count: number;
}
type ApplicationTurnState = TurnState<ConversationState>

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
const storage = new MemoryStorage()

const downloader = new AttachmentDownloader()

const agentApp = new AgentApplication<ApplicationTurnState>({
  storage,
  fileDownloaders: [downloader]
})

const FULL_TEXT = "This is a streaming response."
const CHUNKS = FULL_TEXT.split(" ")

agentApp.onMessage("/stream", async (context: TurnContext, state: ApplicationTurnState) => {
  
  await context.streamingResponse.queueInformativeUpdate("Starting stream...")
  await sleep(1000)

  for (let i: number = 0; i < CHUNKS.length; i++) {
    await context.streamingResponse.queueTextChunk(`Chunk ${i + 1} of ${CHUNKS.length}`)
    if (i < CHUNKS.length - 1) {
      await sleep(1000)
    }
  }

  await context.streamingResponse.endStream()
})

// Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, state: ApplicationTurnState) => {
  // Increment count state
  let count = state.conversation.count ?? 0
  state.conversation.count = ++count

  // Echo back users message
  await context.sendActivity(`[${count}] You said: ${context.activity.text}`)
})

startServer(agentApp)
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { DefaultAzureCredential } from '@azure/identity';
import { ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplication, AttachmentDownloader, TurnContext, TurnState } from '@microsoft/agents-hosting';
import { startServer } from '@microsoft/agents-hosting-express';
import { BlobsStorage } from '@microsoft/agents-hosting-storage-blob';
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

const CONTAINER_NAME = process.env.BLOB_CONTAINER_NAME ?? "";

const storage = new BlobsStorage(
  CONTAINER_NAME,
  undefined,
  undefined,
  process.env.BLOB_STORAGE_ACCOUNT_URL + CONTAINER_NAME,
  new DefaultAzureCredential()
)

const downloader = new AttachmentDownloader()

const agentApp = new AgentApplication<ApplicationTurnState>({
  storage,
  fileDownloaders: [downloader]
})

// Display a welcome message when members are added
agentApp.onConversationUpdate('membersAdded', async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity('Hello and Welcome!')
})

agentApp.onMessage("/stream", async (context: TurnContext, state: ApplicationTurnState) => {

  const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms))
  const FULL_TEXT = "This is a streaming response."
  const CHUNKS = FULL_TEXT.split(" ")
  
  await context.streamingResponse.queueInformativeUpdate("Starting stream...")
  await sleep(1000)

  for (let i: number = 0; i < CHUNKS.length; i++) {
    await context.streamingResponse.queueTextChunk(CHUNKS[i])
    if (i < CHUNKS.length - 1) {
      // in this SDK, 1000ms between queueTextChunk calls does not guarantee that each chunk will have its own Activity
      await sleep(1500)
    }
  }

  await context.streamingResponse.endStream()
})

agentApp.onMessage("/language", async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity("NODEJS")
})

agentApp.onMessage('/error', async (context: TurnContext, state: ApplicationTurnState) => {
  throw new Error('This is a test error triggered by the /error command')
})

// Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity(`You said: ${context.activity.text}`)
})

agentApp.onError(async (context: TurnContext, error: Error) => {
  await context.sendActivity('The bot encountered an error or bug.')
})

startServer(agentApp)
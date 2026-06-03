// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { DefaultAzureCredential } from '@azure/identity';
import { ActivityTypes } from '@microsoft/agents-activity';
import { AgentApplication, AttachmentDownloader, TurnContext, TurnState } from '@microsoft/agents-hosting';
import { startServer } from '@microsoft/agents-hosting-express';
import { CosmosDbPartitionedStorage } from '@microsoft/agents-hosting-storage-cosmos';
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

if (!process.env.COSMOS_DB_DATABASE_ID) {
  throw new Error('COSMOS_DB_DATABASE_ID environment variable is not set')
}
if (!process.env.COSMOS_DB_CONTAINER_ID) {
  throw new Error('COSMOS_DB_CONTAINER_ID environment variable is not set')
}
if (!process.env.COSMOS_DB_ENDPOINT) {
  throw new Error('COSMOS_DB_ENDPOINT environment variable is not set')
}

const credential = new DefaultAzureCredential()
const storage = new CosmosDbPartitionedStorage({
  databaseId: process.env.COSMOS_DB_DATABASE_ID ?? '',
  containerId: process.env.COSMOS_DB_CONTAINER_ID ?? '',
  cosmosClientOptions: {
    endpoint: process.env.COSMOS_DB_ENDPOINT ?? '',
    tokenProvider: async () => {
      const token = await credential.getToken('https://cosmos.azure.com/.default')
      return `type=aad&ver=1.0&sig=${token!.token}`
    }
  }
})

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
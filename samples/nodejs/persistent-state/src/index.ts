// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { startServer } from '@microsoft/agents-hosting-express'
import { TurnState, TurnContext, AgentApplication } from '@microsoft/agents-hosting'
import { ActivityTypes } from '@microsoft/agents-activity'
import { BlobsStorage } from '@microsoft/agents-hosting-storage-blob'

// Create custom conversation state properties.  This is
// used to store conversation properties that persist across agent restarts.
interface ConversationState {
  count: number;
}
type ApplicationTurnState = TurnState<ConversationState>

// Read Azure Blob Storage configuration from environment variables.
// Set AZURE_BLOB_STORAGE_CONNECTION_STRING in your .env file or environment.
// For local development with Azurite, use: UseDevelopmentStorage=true
const connectionString = process.env.AZURE_BLOB_STORAGE_CONNECTION_STRING
if (!connectionString) {
  throw new Error(
    'AZURE_BLOB_STORAGE_CONNECTION_STRING is required. ' +
    'Set it in your .env file or environment variables. ' +
    'For local development with Azurite, use: UseDevelopmentStorage=true'
  )
}

const containerName = process.env.AZURE_BLOB_STORAGE_CONTAINER_NAME ?? 'agents-persistent-state'

// Register IStorage backed by Azure Blob Storage so that conversation state
// persists across Agent restarts and operates correctly in a cluster.
// The container is created automatically on first use.
const storage = new BlobsStorage(containerName, connectionString)

const agentApp = new AgentApplication<ApplicationTurnState>({
  storage
})

// Display a welcome message when members are added
agentApp.onConversationUpdate('membersAdded', async (context: TurnContext, state: ApplicationTurnState) => {
  await context.sendActivity('Hello and Welcome!')
})

// Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, state: ApplicationTurnState) => {
  // Increment count state — persisted to Blob Storage on each turn
  let count = state.conversation.count ?? 0
  state.conversation.count = ++count

  // Echo back users message with the persistent counter
  await context.sendActivity(`[${count}] You said: ${context.activity.text}`)
})

startServer(agentApp)

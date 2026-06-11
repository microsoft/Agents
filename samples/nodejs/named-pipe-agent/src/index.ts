// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { AgentApplication, MemoryStorage, TurnContext, TurnState } from '@microsoft/agents-hosting'
import { createLocalAdapter, startNamedPipeServer } from '@microsoft/agents-hosting-directline-namedpipes'

// Create custom conversation state properties.  This is
// used to store customer properties in conversation state.
interface ConversationState {
  count: number;
}
type ApplicationTurnState = TurnState<ConversationState>

// Named-pipe (DirectLineFlex) echo agent.
//
// Communicates exclusively over named pipes via the DirectLine protocol.  No HTTP
// endpoint is exposed and no Azure/Entra credentials are required.  This is the
// canonical shape for agents deployed behind the DirectLine App Service extension
// (DirectLineFlex), where the sidecar handles external authentication and relays
// traffic over the pipe.
class NamedPipeAgent extends AgentApplication<ApplicationTurnState> {
  constructor () {
    // Register IStorage.  For development, MemoryStorage is suitable.
    // For production Agents, persisted storage should be used so that state
    // survives Agent restarts, and operates correctly in a cluster of instances.
    super({ startTypingTimer: false, storage: new MemoryStorage() })

    this.onConversationUpdate('membersAdded', this.welcome)
    this.onActivity('message', this.echo)
  }

  // Display a welcome message when members are added.
  welcome = async (context: TurnContext) => {
    for (const member of context.activity.membersAdded ?? []) {
      if (member.id !== context.activity.recipient?.id) {
        await context.sendActivity('Hello and Welcome! I am a named-pipe agent.')
      }
    }
  }

  // Echo back the user's message, prefixed with a per-conversation counter.
  echo = async (context: TurnContext, state: ApplicationTurnState) => {
    let count = state.conversation.count ?? 0
    state.conversation.count = ++count
    await context.sendActivity(`[${count}] You said: ${context.activity.text}`)
  }
}

// --- Startup ---

const PIPE_NAME = process.env.PIPE_NAME || 'bfv4.pipes'

// createLocalAdapter() builds a CloudAdapter configured for pipe-only use (no credentials).
const adapter = createLocalAdapter()
const agent = new NamedPipeAgent()

const service = await startNamedPipeServer(adapter, (context) => agent.run(context), {
  pipeName: PIPE_NAME
})

service.ready.then(() => {
  console.log(`Named pipe agent connected on '${PIPE_NAME}'`)
}).catch(() => {
  // Ready rejection means stop() was called before connecting — normal during shutdown.
})

console.log(`Named pipe agent started, waiting for connection on '${PIPE_NAME}'...`)

// Graceful shutdown — handle both SIGINT (Ctrl-C) and SIGTERM (App Service / container).
let shuttingDown = false
const shutdown = async (signal: string) => {
  if (shuttingDown) return
  shuttingDown = true
  console.log(`Received ${signal}, shutting down...`)
  try {
    await service.stop()
  } catch (err) {
    console.error('Error during shutdown:', err)
  }
  process.exit(0)
}

process.once('SIGINT', () => { shutdown('SIGINT').catch(() => {}) })
process.once('SIGTERM', () => { shutdown('SIGTERM').catch(() => {}) })

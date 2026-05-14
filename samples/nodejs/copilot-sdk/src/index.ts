// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { TurnState, MemoryStorage, TurnContext, AgentApplication } from '@microsoft/agents-hosting'
import { ActivityTypes } from '@microsoft/agents-activity'
import { CopilotClient, approveAll, type CopilotSession } from '@github/copilot-sdk'
import { createRollDiceTool } from './tools/diceRoller.js'
import { createInventoryTool } from './tools/inventoryManager.js'

const DUNGEON_SCRIBE_PERSONA = `You are the Dungeon Scribe, a dramatic and theatrical fantasy narrator who serves as the party's faithful record-keeper. You speak with flair and gravitas, using vivid fantasy language.

When rolling dice, always use the roll_dice tool — never simulate rolls yourself.
When managing inventory, always use the manage_inventory tool.

Keep responses concise but flavorful. Use emoji sparingly for emphasis (🎲⚔️🗡️🐉🏰📦🎒🗺️).`

let copilotClient: CopilotClient | null = null
// Reuse Copilot sessions per user+conversation for multi-turn context
const sessions = new Map<string, CopilotSession>()
const sessionInitPromises = new Map<string, Promise<CopilotSession>>()

function getCopilotClient (): CopilotClient {
  if (!copilotClient) {
    copilotClient = new CopilotClient()
  }
  return copilotClient
}

async function getOrCreateSession (client: CopilotClient, sessionKey: string, gitHubToken?: string): Promise<CopilotSession> {
  const existing = sessions.get(sessionKey)
  if (existing) return existing

  // Prevent duplicate session creation for the same key
  const pending = sessionInitPromises.get(sessionKey)
  if (pending) return pending

  const promise = (async () => {
    try {
      const model = process.env.COPILOT_MODEL ?? 'gpt-4.1'
      const rollDice = createRollDiceTool()
      const inventoryTool = createInventoryTool(sessionKey)

      const session = await client.createSession({
        model,
        onPermissionRequest: approveAll,
        tools: [rollDice, inventoryTool],
        streaming: true,
        gitHubToken,
        systemMessage: { content: DUNGEON_SCRIBE_PERSONA },
      })

      sessions.set(sessionKey, session)
      return session
    } finally {
      sessionInitPromises.delete(sessionKey)
    }
  })()

  sessionInitPromises.set(sessionKey, promise)
  return promise
}

const storage = new MemoryStorage()
const agentApp = new AgentApplication<TurnState>({ storage })

agentApp.onConversationUpdate('membersAdded', async (context: TurnContext, _state: TurnState) => {
  await context.sendActivity(
    '⚔️ *The Dungeon Scribe unfurls a weathered scroll and dips quill in ink...*\n\n' +
    'Hail, brave adventurer! I am the **Dungeon Scribe**, keeper of quests and chronicler of legends.\n\n' +
    'I can:\n' +
    '- 🎲 **Roll dice** — just say something like \'roll 2d6+3\'\n' +
    '- 📦 **Manage inventory** — \'add Sword of Truth to inventory\'\n' +
    '- 🗺️ **Narrate your adventures** — describe scenes, locations, encounters\n\n' +
    'What tale shall we weave today?'
  )
})

// Sign-out command to reset the GitHub OAuth token
agentApp.onMessage('-signout', async (context: TurnContext, state: TurnState) => {
  await agentApp.authorization.signOut(context, state)
  // Remove cached sessions for this user so a fresh token is used on next sign-in
  const userId = context.activity.from?.id ?? 'anonymous'
  for (const key of sessions.keys()) {
    if (key.startsWith(`${userId}:`)) {
      sessions.delete(key)
    }
  }
  await context.sendActivity('📜 *The Scribe closes the scroll…* You have been signed out. Send any message to sign in again.')
})

agentApp.authorization.onSignInFailure(async (context: TurnContext, _state: TurnState, authId?: string, err?: string) => {
  await context.sendActivity(`⚠️ *The Scribe cannot verify your identity.* Sign-in failed for '${authId ?? 'unknown'}': ${err ?? 'unknown error'}`)
})

agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, _state: TurnState) => {
  const userText = context.activity.text
  if (!userText) return

  // Let streaming-capable clients know we're working on a response
  context.streamingResponse.queueInformativeUpdate('The gods confer… stand fast a moment.')

  // Get the user's GitHub OAuth token (acquired by AutoSignIn via Azure Bot OAuth Connection)
  const tokenResponse = await agentApp.authorization.getToken(context, 'github')
  const gitHubToken = tokenResponse?.token

  // Key sessions by user + conversation so each user gets their own Copilot identity
  const userId = context.activity.from?.id ?? 'anonymous'
  const conversationId = context.activity.conversation?.id ?? 'default'
  const sessionKey = `${userId}:${conversationId}`

  try {
    const client = getCopilotClient()
    const session = await getOrCreateSession(client, sessionKey, gitHubToken)

    let anyDeltas = false

    const done = new Promise<void>((resolve, reject) => {
      let settled = false
      const unsubscribe = session.on((event) => {
        if (settled) return
        switch (event.type) {
          case 'assistant.message_delta': {
            const delta = (event as any).data?.deltaContent
            if (delta && delta.length > 0) {
              anyDeltas = true
              context.streamingResponse.queueTextChunk(delta)
            }
            break
          }
          case 'session.idle':
            settled = true
            unsubscribe()
            resolve()
            break
          case 'session.error':
            settled = true
            unsubscribe()
            reject(new Error(`Session error: ${(event as any).data?.message ?? 'unknown error'}`))
            break
        }
      })
    })

    await session.send({ prompt: userText })
    await done

    if (!anyDeltas) {
      await context.sendActivity(
        "📜 *The Scribe's quill hesitates...* I couldn't conjure a response. Try again?"
      )
    }
  } catch (err) {
    // Discard the cached session on error so it gets recreated next turn
    sessions.delete(sessionKey)
    console.error('Copilot SDK error:', err)
    await context.sendActivity(
      '⚠️ *A magical disturbance disrupts the Scribe\'s work.* ' +
      'Verify that you signed in with a GitHub account that has an active Copilot subscription, then try again.'
    )
  } finally {
    await context.streamingResponse.endStream()
  }
})

startServer(agentApp)

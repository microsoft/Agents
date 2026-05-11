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
// Reuse Copilot sessions per conversation for multi-turn context
const sessions = new Map<string, CopilotSession>()

async function getCopilotClient(): Promise<CopilotClient> {
  if (!copilotClient) {
    copilotClient = new CopilotClient()
  }
  return copilotClient
}

async function getOrCreateSession(client: CopilotClient, conversationId: string): Promise<CopilotSession> {
  const existing = sessions.get(conversationId)
  if (existing) return existing

  const model = process.env.COPILOT_MODEL ?? 'gpt-4.1'
  const rollDice = createRollDiceTool()
  const inventoryTool = createInventoryTool(conversationId)

  const session = await client.createSession({
    model,
    onPermissionRequest: approveAll,
    tools: [rollDice, inventoryTool],
    systemMessage: { content: DUNGEON_SCRIBE_PERSONA },
  })

  sessions.set(conversationId, session)
  return session
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

agentApp.onActivity(ActivityTypes.Message, async (context: TurnContext, _state: TurnState) => {
  const userText = context.activity.text
  if (!userText) return

  const conversationId = context.activity.conversation?.id ?? 'default'

  try {
    const client = await getCopilotClient()
    const session = await getOrCreateSession(client, conversationId)

    const response = await session.sendAndWait({ prompt: userText })

    if (response?.data?.content) {
      await context.sendActivity(response.data.content)
    } else {
      await context.sendActivity(
        "📜 *The Scribe's quill hesitates...* I couldn't conjure a response. Try again?"
      )
    }
  } catch (err) {
    // Discard the cached session on error so it gets recreated next turn
    sessions.delete(conversationId)
    console.error('Copilot SDK error:', err)
    await context.sendActivity(
      '⚠️ *A magical disturbance disrupts the Scribe\'s work.* ' +
      'Please ensure the Copilot CLI is installed and you are logged in.\n\n' +
      'Run: `npm install -g @github/copilot && copilot auth login`'
    )
  }
})

startServer(agentApp)
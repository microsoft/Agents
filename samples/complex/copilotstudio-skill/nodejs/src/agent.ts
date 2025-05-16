import { AgentApplication, MessageFactory } from '@microsoft/agents-hosting'
import pjson from '@microsoft/agents-hosting/package.json'

export const skillAgent = new AgentApplication()
skillAgent.onConversationUpdate('membersAdded', async (context) => {
  const welcomeText = `Hello from echo bot, running on version ${pjson.version}`
  await context.sendActivity(MessageFactory.text(welcomeText, welcomeText))
})
skillAgent.onActivity('message', async (context) => {
  const text = context.activity.text
  const replyText = `Echo: ${text}`
  await context.sendActivity(MessageFactory.text(replyText, replyText))
  if (text?.includes('version')) {
    await context.sendActivity(MessageFactory.text('Running on version ' + pjson.version, 'Running on version ' + pjson.version))
  }
})

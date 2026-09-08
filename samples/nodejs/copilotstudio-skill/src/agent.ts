import express from 'express'
import path from 'path'
import { AgentApplication, MessageFactory, CloudAdapter, authorizeJWT, getAuthConfigWithDefaults } from '@microsoft/agents-hosting'
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

skillAgent.onActivity('endOfConversation', async (context) => {
  // Handle PVASkillImport ping and endOfConversation from Copilot Studio
})

const authConfig = getAuthConfigWithDefaults()
const adapter = skillAgent.adapter || new CloudAdapter()

const server = express()
server.use(express.json())

// Serve static files BEFORE auth so /manifest.json is publicly accessible
server.use(express.static(path.join(__dirname, '..', 'public')))

// Auth only on the messages endpoint
server.post('/api/messages', authorizeJWT(authConfig), (req, res) =>
  adapter.process(req, res, (context) => skillAgent.run(context))
)

const port = process.env.PORT || 3978
server.listen(port, () => {
  console.log(`\nServer listening to port ${port} on sdk ${pjson.version} for appId ${authConfig.clientId} debug ${process.env.DEBUG}`)
})

import { authorizeJWT, CloudAdapter, loadAuthConfigFromEnv } from '@microsoft/agents-hosting'
import express from 'express'
import { weatherAgent } from './myAgent.js'

const authConfig = loadAuthConfigFromEnv()
const adapter = new CloudAdapter(authConfig)

const server = express()
server.use(express.json())
server.use(authorizeJWT(authConfig))

server.post('/api/messages', async (req, res) => {
  await adapter.process(req, res, async (context) => {
    await weatherAgent.run(context)
  })
})

const port = process.env.PORT || 3978
server.listen(port, () => {
  console.log(`\nServer listening to port ${port} for appId ${authConfig.clientId} debug ${process.env.DEBUG}`)
}).on('error', (err) => {
  console.error(err)
})

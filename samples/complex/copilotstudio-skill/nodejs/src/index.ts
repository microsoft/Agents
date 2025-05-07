import express, { json } from 'express'

import { CloudAdapter, loadAuthConfigFromEnv, authorizeJWT } from '@microsoft/agents-hosting'
import pjson from '@microsoft/agents-hosting/package.json'

import { EchoBot } from './agent.js'

const config = loadAuthConfigFromEnv()
const adapter = new CloudAdapter(config)
const myBot = new EchoBot()

const server = express()
server.use(express.static('public'))
server.use(authorizeJWT(config))

server.use(json())
server.post('/api/messages',
  async (req, res) => {
    await adapter.process(req, res, (context) => myBot.run(context))
  }
)

const port = process.env.PORT || 3978

server.listen(port, () => {
  console.log(`\n agent skill, running on sdk version ${pjson.version} lisenting on ${port} for bot ${process.env.clientId}`)
}).on('error', (err) => {
  console.error(err)
  process.exit(1)
})

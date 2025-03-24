import { AuthConfiguration, authorizeJWT, CloudAdapter, loadAuthConfigFromEnv, Request } from '@microsoft/agents-hosting'
import express, { Response } from 'express'
import { WeatherAgent } from './myAgent.js'
const authConfig: AuthConfiguration = loadAuthConfigFromEnv()
const adapter = new CloudAdapter(authConfig)

const server = express()
server.use(express.json())
server.use(authorizeJWT(authConfig))

server.post('/api/messages', async (req: Request, res: Response) => {
  await adapter.process(req, res, async (context) => {
    await WeatherAgent.run(context)
  })
})

const port = process.env.PORT || 3978
server.listen(port, () => {
  console.log(`\nServer listening to port ${port} for appId ${authConfig.clientId} debug ${process.env.DEBUG}`)
})

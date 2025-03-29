import { AuthConfiguration, authorizeJWT, CloudAdapter, loadAuthConfigFromEnv, Request } from '@microsoft/agents-hosting'
import express, { Response } from 'express'
import { agentApp } from './agent'

const authConfig: AuthConfiguration = loadAuthConfigFromEnv()
const adapter = new CloudAdapter(authConfig)

const server = express()
server.use(express.json())
server.use(authorizeJWT(authConfig))

server.post('/api/messages', async (req: Request, res: Response) => {
  await adapter.process(req, res, async (context) => {
    const app = agentApp
    await app.run(context)
  })
})

const port = process.env.PORT || 3978
server.listen(port, () => {
  console.log(`\nServer listening to port ${port} for appId ${authConfig.clientId} debug ${process.env.DEBUG}`)
}).on('error', (err) => {
  console.error(err)
  process.exit(1)
})

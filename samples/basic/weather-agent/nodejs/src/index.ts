import { startServer } from '@microsoft/agents-hosting-express'
import { weatherAgent } from './myAgent.js'

startServer(weatherAgent)

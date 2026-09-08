// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import express, { NextFunction, Request, Response } from 'express'
import { startServer } from '@microsoft/agents-hosting-express'
import {
  AgentApplication,
  authorizeJWT,
  CloudAdapter,
  CardFactory,
  Conversation,
  ConversationClaims,
  loadAuthConfigFromEnv,
  MemoryStorage,
  Proactive,
  TurnContext,
  TurnState
} from '@microsoft/agents-hosting'
import { Activity, ActivityTypes, ConversationReference } from '@microsoft/agents-activity'

const storage = new MemoryStorage()

const welcomeCard = {
  type: 'AdaptiveCard',
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  version: '1.5',
  body: [
    { type: 'TextBlock', text: 'Welcome to the Proactive sample.', weight: 'Bolder', size: 'Medium', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: 'Commands:', weight: 'Bolder', spacing: 'Medium', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: '• -s: Store this conversation.', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: '• -c: Continue this conversation proactively.', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: '• -c <conversation-id>: Continue a stored conversation.', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: '• -convo: Show the conversation data for the HTTP example.', wrap: true, horizontalAlignment: 'Left' },
    { type: 'TextBlock', text: 'Send other text to echo it from a proactive turn.', spacing: 'Medium', wrap: true, horizontalAlignment: 'Left' }
  ]
}

class ProactiveAgent extends AgentApplication<TurnState> {
  constructor () {
    super({ storage, proactive: { storage } })

    this.onConversationUpdate('membersAdded', async (context: TurnContext) => {
      const agentId = context.activity.recipient?.id
      if (context.activity.membersAdded?.some(member => member.id !== agentId)) {
        await context.sendActivity(Activity.fromObject({
          type: ActivityTypes.Message,
          attachments: [CardFactory.adaptiveCard(welcomeCard)]
        }))
      }
    })

    this.onMessage('-s', async (context: TurnContext) => {
      const conversationId = await this.proactive.storeConversation(new Conversation(context))
      await context.sendActivity(
        `Your conversation has been stored. Send a POST request to /proactive/sendActivity/${conversationId} to trigger a proactive message.`
      )
    })

    this.onMessage('-convo', async (context: TurnContext) => {
      await context.sendActivity(new Conversation(context).toJson())
    })

    this.onMessage(/^-c(?:\s+\S+)?\s*$/, async (context: TurnContext) => {
      const parts = (context.activity.text ?? '').trim().split(/\s+/)
      let conversation: Conversation

      if (parts.length === 1) {
        conversation = new Conversation(context)
      } else {
        const conversationId = parts[1]
        const storedConversation = await this.proactive.getConversation(conversationId)
        if (!storedConversation) {
          await context.sendActivity(`Conversation '${conversationId}' was not found. Send -s first to store it.`)
          return
        }
        conversation = storedConversation
      }

      await this.proactive.continueConversation(
        this.adapter,
        conversation,
        this.onContinueConversation.bind(this)
      )
    })

    this.onActivity(ActivityTypes.Message, async (context: TurnContext) => {
      const conversation = new Conversation(context)
      await this.proactive.continueConversation(
        this.adapter,
        conversation,
        async (continuedContext: TurnContext) => {
          const originalActivity = continuedContext.activity.value as Activity
          await continuedContext.sendActivity(`You said: ${originalActivity.text}`)
        },
        undefined,
        {
          value: context.activity,
          valueType: Proactive.ContinueConversationValueType
        }
      )
    })
  }

  async onContinueConversation (context: TurnContext): Promise<void> {
    await context.sendActivity('This is OnContinueConversation')
  }
}

interface CallerClaims {
  appid?: string
  azp?: string
}

interface ContinuePayload {
  reference?: ConversationReference
  claims?: ConversationClaims
}

const authConfig = loadAuthConfigFromEnv()
const isDevelopment = (process.env.NODE_ENV ?? 'production').toLowerCase() === 'development'
const allowedCallers = (process.env.ALLOWED_CALLERS ?? '')
  .split(',')
  .map(value => value.trim())
  .filter(Boolean)

if (!isDevelopment && allowedCallers.length === 0) {
  throw new Error('ALLOWED_CALLERS must contain at least one caller app ID outside Development.')
}

const requireAllowedCaller = (req: Request, res: Response, next: NextFunction): void => {
  const claims = (req as Request & { user?: CallerClaims }).user
  const callerId = claims?.appid ?? claims?.azp ?? ''

  if (!allowedCallers.includes(callerId)) {
    res.status(403).json({ error: `Caller '${callerId}' is not in the allowed callers list.` })
    return
  }

  next()
}

const proactiveMiddleware = isDevelopment
  ? []
  : [authorizeJWT(authConfig), requireAllowedCaller]

const agent = new ProactiveAgent()
const adapter = agent.adapter as CloudAdapter

startServer(agent, {
  authConfig,
  beforeListen: (server: express.Express) => {
    server.get('/', (_req: Request, res: Response) => {
      res.json({ status: 'ready', sample: 'proactive' })
    })

    server.post(
      '/proactive/sendActivity/:conversationId',
      ...proactiveMiddleware,
      async (req: Request, res: Response) => {
        const conversationIdParam = req.params.conversationId
        const conversationId = Array.isArray(conversationIdParam)
          ? conversationIdParam[0]
          : conversationIdParam
        if (
          !req.body ||
          typeof req.body !== 'object' ||
          Array.isArray(req.body) ||
          Object.keys(req.body as Record<string, unknown>).length === 0
        ) {
          res.status(400).json({ error: 'The request body must be an activity object.' })
          return
        }

        try {
          await agent.proactive.sendActivity(adapter, conversationId, req.body as Partial<Activity>)
          res.status(200).json({ status: 'ok', conversationId })
        } catch (error: unknown) {
          const message = error instanceof Error ? error.message : String(error)
          const status = message.toLowerCase().includes('not found') ? 404 : 500
          if (status === 404) {
            res.status(404).json({
              error: `Conversation '${conversationId}' was not found. Send -s first to store it.`
            })
          } else {
            console.error('Failed to send proactive activity:', error)
            res.status(500).json({ error: 'The proactive activity could not be sent.' })
          }
        }
      }
    )

    server.post(
      '/proactive/continue',
      ...proactiveMiddleware,
      async (req: Request, res: Response) => {
        const payload = req.body as ContinuePayload
        if (!payload?.reference || !payload?.claims) {
          res.status(400).json({ error: "The request body must contain 'reference' and 'claims'." })
          return
        }

        let conversation: Conversation
        try {
          conversation = new Conversation(payload.claims, payload.reference)
          conversation.validate()
        } catch (error: unknown) {
          console.error('Invalid conversation data:', error)
          res.status(400).json({ error: 'The conversation data is invalid.' })
          return
        }

        try {
          await agent.proactive.continueConversation(
            adapter,
            conversation,
            agent.onContinueConversation.bind(agent)
          )
          res.status(200).json({
            status: 'ok',
            conversationId: conversation.reference.conversation.id
          })
        } catch (error: unknown) {
          console.error('Failed to continue conversation:', error)
          res.status(500).json({ error: 'The conversation could not be continued.' })
        }
      }
    )

    server.use((error: unknown, _req: Request, res: Response, next: NextFunction) => {
      if (error instanceof SyntaxError) {
        res.status(400).json({ error: 'The request body must contain valid JSON.' })
        return
      }
      next(error)
    })
  }
})

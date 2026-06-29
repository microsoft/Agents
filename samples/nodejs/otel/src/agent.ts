// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, MemoryStorage, RouteRank, TurnContext, TurnState } from '@microsoft/agents-hosting'
import { SpanStatusCode, type Span } from '@opentelemetry/api'
import { AgentTelemetry } from './agentTelemetry'

class OTelAgent extends AgentApplication<TurnState> {
  private tracer = AgentTelemetry.tracer

  constructor () {
    super({ startTypingTimer: true, storage: new MemoryStorage() })

    this.onConversationUpdate('membersAdded', this.welcome)
    this.onActivity('message', this.message, undefined, RouteRank.Last)
    this.onError(this.error)
  }

  welcome = async (ctx: TurnContext) => {
    return this.tracer.startActiveSpan('agent.welcome_message', async (span: Span) => {
      try {
        span.setAttribute('conversation.id', ctx.activity.conversation?.id ?? 'unknown')
        span.setAttribute('channel.id', ctx.activity.channelId ?? 'unknown')
        span.setAttribute('members.added.count', ctx.activity.membersAdded?.length ?? 0)

        ctx.activity.membersAdded?.forEach((member) => {
          if (member.id !== ctx.activity.recipient?.id) {
            span.addEvent(
              'member.added',
              {
                'member.id': member.id,
                'member.name': member.name,
              },
              Date.now())
          }
        })
        await ctx.sendActivity('Hello and Welcome!')

        AgentTelemetry.routeExecutedCounter.add(1,
          {
            'route.type': 'welcome_message',
            'conversation.id': ctx.activity.conversation?.id ?? 'unknown'
          }
        )
        AgentTelemetry.logInfo(`Welcome message sent for conversation ${ctx.activity.conversation?.id ?? 'unknown'}`, {})

        span.setStatus({ code: SpanStatusCode.OK })
      } catch (error) {
        if (error instanceof Error) {
          span.recordException(error)
          AgentTelemetry.logError(`Welcome message failed for conversation ${ctx.activity.conversation?.id ?? 'unknown'}`, {
            'route.type': 'welcome_message',
            'error.message': error.message
          })
        } else {
          span.recordException(new Error(String(error)))
          AgentTelemetry.logError(`Welcome message failed for conversation ${ctx.activity.conversation?.id ?? 'unknown'}`, {
            'route.type': 'welcome_message',
            'error.message': String(error)
          })
        }
        span.setStatus({ code: SpanStatusCode.ERROR })
        throw error
      } finally {
        span.end()
      }
    })
  }

  message = async (ctx: TurnContext) => {
    return this.tracer.startActiveSpan('agent.message_handler', async (span: Span) => {
      const t0 = performance.now()
      const conversationId = ctx.activity.conversation?.id ?? 'unknown'
      const channelId = ctx.activity.channelId ?? 'unknown'
      let status = 'success'

      try {
        span.setAttribute('conversation.id', conversationId)
        span.setAttribute('channel.id', channelId)
        span.setAttribute('message.text.length', ctx.activity.text?.length ?? 0)
        span.setAttribute('user.id', ctx.activity.from?.id ?? 'unknown')

        span.addEvent(
          'message.received',
          {
            'message.id': ctx.activity.id,
            'message.text': ctx.activity.text,
            'user.id': ctx.activity.from?.id,
            'channel.id': channelId,
          },
          Date.now())

        await ctx.sendActivity(`You said: ${ctx.activity.text}`)
        span.addEvent(
          'response.sent',
          undefined,
          Date.now())

        AgentTelemetry.routeExecutedCounter.add(1,
          {
            'route.type': 'message_handler',
            'conversation.id': conversationId
          })
        AgentTelemetry.logInfo('Message handled', {
          'conversation.id': conversationId,
          'route.type': 'message_handler'
        })
        status = 'success'
        span.setStatus({ code: SpanStatusCode.OK })
      } catch (error) {
        let errorMessage: string
        if (error instanceof Error) {
          span.recordException(error)
          errorMessage = error.message
        } else {
          span.recordException(new Error(String(error)))
          errorMessage = String(error)
        }
        AgentTelemetry.logError(`Message handling failed for conversation ${conversationId}`, {
          'route.type': 'message_handler',
          'error.message': errorMessage
        })
        span.setStatus({ code: SpanStatusCode.ERROR })
        status = 'error'
        throw error
      } finally {
        const processedMs = performance.now() - t0
        AgentTelemetry.messageProcessingDuration.record(processedMs,
          {
            'conversation.id': conversationId,
            'channel.id': channelId,
            status
          })
        span.end()
      }
    })
  }

  error = async (ctx: TurnContext, error: Error) => {
    AgentTelemetry.logError(`Unhandled error in conversation ${ctx.activity.conversation?.id ?? 'unknown'}`, {
      'error.message': error.message
    })
    // Send a message to the user
    await ctx.sendActivity('The bot encountered an error or bug.')
  }
}

startServer(new OTelAgent())

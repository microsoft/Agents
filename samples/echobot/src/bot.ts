// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { ActivityHandler, MessageFactory } from '@microsoft/agents-bot-hosting'

export class EchoBot extends ActivityHandler {
  constructor() {
    super()
    this.onMessage(async (context, next) => {
      const replyText = `Echo: ${context.activity.text}`
      await context.sendActivity(MessageFactory.text(replyText, replyText))
      await next()
    })

    this.onMembersAdded(async (context, next) => {
      const membersAdded = context.activity.membersAdded ?? []
      const welcomeText = 'Hello and welcome!'
      for (const member of membersAdded) {
        if (member.id !== (context.activity.recipient?.id ?? '')) {
          await context.sendActivity(MessageFactory.text(welcomeText, welcomeText))
        }
      }
      // By calling next() you ensure that the next BotHandler is run.
      await next()
    })
  }
}

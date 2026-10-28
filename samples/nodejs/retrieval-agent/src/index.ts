// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, MemoryStorage, MessageFactory, TurnContext, TurnState } from '@microsoft/agents-hosting'
import { Activity } from '@microsoft/agents-activity'
import { handleBuildGenieMessage, buildGenieResponses } from './services/messageRoute.js'
import { getRetrievalOptions, retrieveSharePoint } from './services/retrievalClient.js'

class RetrievalAgent extends AgentApplication<TurnState> {
  constructor () {
    super({ storage: new MemoryStorage() })
    getRetrievalOptions()
    this.authorization.onSignInFailure(this.signInFailure)
    this.onConversationUpdate('membersAdded', this.welcome)
    this.onActivity('message', this.message, ['graph'])
  }

  private welcome = async (context: TurnContext): Promise<void> => {
    if (context.activity.membersAdded?.some(member => member.id !== context.activity.recipient?.id)) {
      await context.sendActivity(MessageFactory.text('Hello! I am Build Genie. Ask me about Build 2025 sessions in the configured SharePoint site. I only search content you can access.'))
    }
  }

  private message = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(Activity.fromObject({ type: 'typing' }))
    const result = await retrieveSharePoint(
      context.activity.text ?? '',
      async () => (await this.authorization.getToken(context, 'graph'))?.token
    )
    await handleBuildGenieMessage(
      result,
      async text => await context.sendActivity(MessageFactory.text(text)),
      async card => await context.sendActivity(MessageFactory.attachment({ contentType: 'application/vnd.microsoft.card.adaptive', content: card }))
    )
  }

  private signInFailure = async (context: TurnContext): Promise<void> => {
    await context.sendActivity(MessageFactory.text(buildGenieResponses.notSignedIn))
  }
}

startServer(new RetrievalAgent())

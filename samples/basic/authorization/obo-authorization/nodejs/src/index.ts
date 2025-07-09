// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, MemoryStorage, MessageFactory, TurnContext, TurnState } from '@microsoft/agents-hosting'

class OboApp extends AgentApplication<TurnState> {
  constructor () {
    super({
      storage: new MemoryStorage(),
      authorization: {
        graph: { text: 'Sign in with Microsoft Graph', title: 'Graph Sign In' }
      }
    })
    this.onConversationUpdate('membersAdded', this._status)
    this.authorization.onSignInSuccess(this._singinSuccess)
    this.onMessage('/status', this._status)
    this.onMessage('/logout', this._logout)
    this.onActivity('message', this._message, ['graph'])
  }

  private _status = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('Welcome to the OBO Auth App demo!'))
    const tresp = await this.authorization.getToken(context, 'graph')
    if (tresp) {
      await context.sendActivity(MessageFactory.text('Token received: ' + tresp.token?.length))
    } else {
      await context.sendActivity(MessageFactory.text('Token request status: ' + tresp || 'unknown'))
    }
    const oboToken = await this.authorization.exchangeToken(context, ['https://graph.microsoft.com/.default'], 'graph')
    await context.sendActivity(MessageFactory.text('OBO Token received: ' + (oboToken?.token?.length || 0)))
  }

  private _logout = async (context: TurnContext, state: TurnState): Promise<void> => {
    await this.authorization.signOut(context, state)
    await context.sendActivity(MessageFactory.text('user logged out'))
  }

  private _singinSuccess = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('User signed in successfully'))
  }

  private _message = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('You said.' + context.activity.text))
  }
}

startServer(new OboApp())

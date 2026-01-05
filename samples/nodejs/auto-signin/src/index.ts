// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, CardFactory, MemoryStorage, MessageFactory, TurnContext, TurnState } from '@microsoft/agents-hosting'
import { Template } from 'adaptivecards-templating'
import { getUserInfo } from './userGraphClient.js'
import { ActivityTypes } from '@microsoft/agents-activity'

class AutoSignInDemo extends AgentApplication<TurnState> {
  constructor () {
    super({
      storage: new MemoryStorage(),
      authorization: {
        graph: { text: 'Sign in with Microsoft Graph', title: 'Graph Sign In' },
      }
    })
    this.authorization.onSignInFailure(this._singinFailure)
    this.onMessage('-me', this._profileRequest, ['graph'])
    this.onMessage('-logout', this._logout)
    this.onActivity(ActivityTypes.Message, this._message)
  }

  private _logout = async (context: TurnContext, state: TurnState): Promise<void> => {
    await this.authorization.signOut(context, state)
    await context.sendActivity(MessageFactory.text('user logged out'))
  }

  private _singinFailure = async (context: TurnContext, state: TurnState, authId?: string, err?: string): Promise<void> => {
    await context.sendActivity(MessageFactory.text(`Signing Failure in auth handler: ${authId} with error: ${err}`))
  }

  private _message = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('You said.' + context.activity.text))
  }

  private _profileRequest = async (context: TurnContext, state: TurnState): Promise<void> => {
    const userTokenResponse = await this.authorization.getToken(context, 'graph')
    const userTemplate = (await import('./userProfileCard.json', { with: { type: 'json' } })).default
    const template = new Template(userTemplate)
    const userInfo = await getUserInfo(userTokenResponse?.token!)
    const card = template.expand(userInfo)
    const activity = MessageFactory.attachment(CardFactory.adaptiveCard(card))
    await context.sendActivity(activity)
  }
}

startServer(new AutoSignInDemo())

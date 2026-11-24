// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { startServer } from '@microsoft/agents-hosting-express'
import { AgentApplication, MemoryStorage, MessageFactory, TurnContext, TurnState } from '@microsoft/agents-hosting'

class OboApp extends AgentApplication<TurnState> {
  constructor () {
    super({
      storage: new MemoryStorage()
    })
    this.onError(async (context) => {
      await context.sendActivity('Authorization or turn processing failed. Check the OAuth connection, OBO credentials, audience, and consent; then retry.')
    })
    this.onConversationUpdate('membersAdded', this._status)
    this.authorization.onSignInSuccess(this._singinSuccess)
    this.authorization.onSignInFailure(async (context) => {
      await context.sendActivity('Sign-in did not complete. Retry your message, or send CancelledByUser to cancel a pending sign-in.')
    })
    this.onMessage('/status', this._status)
    this.onMessage('/logout', this._logout)
    // The graph handler is configured in .env under AgentApplication__UserAuthorization__Handlers__graph__*.
    this.onActivity('message', this._message, ['graph'])
  }

  private _status = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('Welcome to the OBO Auth App demo!'))
    // With oboScopes configured, getToken returns the downstream Graph token.
    const response = await this.authorization.getToken(context, 'graph')
    await context.sendActivity(response?.token
      ? 'Graph token available. Send a message to request your profile.'
      : 'No Graph token available. Send a message to sign in.')
  }

  private _logout = async (context: TurnContext, state: TurnState): Promise<void> => {
    try {
      // Do not acquire or exchange a token before signing out.
      await this.authorization.signOut(context, state)
      await context.sendActivity(MessageFactory.text('user logged out'))
    } catch {
      await context.sendActivity('Sign-out failed. Retry /logout after checking the OAuth connection.')
    }
  }

  private _singinSuccess = async (context: TurnContext, state: TurnState): Promise<void> => {
    await context.sendActivity(MessageFactory.text('User signed in successfully'))
  }

  private _message = async (context: TurnContext, state: TurnState): Promise<void> => {
    const response = await this.authorization.getToken(context, 'graph')
    if (!response?.token) {
      await context.sendActivity('No Graph token available. Send another message to sign in.')
      return
    }
    try {
      const result = await fetch('https://graph.microsoft.com/v1.0/me?$select=displayName', {
        headers: { Authorization: `Bearer ${response.token}` },
        signal: AbortSignal.timeout(15000)
      })
      if (!result.ok) {
        await context.sendActivity(`Graph profile request failed (HTTP ${result.status}). Check delegated User.Read permission and consent.`)
        return
      }
      let profile: unknown
      try {
        profile = await result.json()
      } catch {
        await context.sendActivity('Graph returned an invalid profile response.')
        return
      }
      if (typeof profile !== 'object' || profile === null || !('displayName' in profile) ||
          typeof profile.displayName !== 'string' || !profile.displayName.trim()) {
        await context.sendActivity('Graph returned an invalid profile response.')
        return
      }
      await context.sendActivity(MessageFactory.text('You said.' + context.activity.text))
      await context.sendActivity(MessageFactory.text('Your Graph profile: ' + profile.displayName))
    } catch {
      await context.sendActivity('Graph profile request could not be completed. Check network access and retry.')
    }
  }
}

startServer(new OboApp())

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { ActivityTypes } from '@microsoft/agents-activity'
import { TurnContext, AgentApplication, TurnState } from '@microsoft/agents-hosting'

export class DialogAgent extends AgentApplication<TurnState> {
  constructor () {
    super()
    this.onMessage('quit', this._quit)
    this.onActivity(ActivityTypes.Message, this._echo)
  }

  private async _quit (context: TurnContext, state: TurnState): Promise<void> {
    await context.sendActivity('> Bye!')
    process.exit()
  }

  private async _echo (context: TurnContext, state: TurnState): Promise<void> {
    await context.sendActivity(`> I heard you say "${context.activity.text}"`)
  }
}

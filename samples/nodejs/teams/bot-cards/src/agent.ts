// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { AgentApplication, MemoryStorage } from '@microsoft/agents-hosting'
import { Activity } from '@microsoft/agents-activity'
import { TeamsAgentExtension } from '@microsoft/agents-hosting-extensions-msteams'

function cardActions () {
  const inner = {
    type: 'AdaptiveCard',
    body: [{ type: 'TextBlock', text: '**Welcome To Your New Card**' }, {
      type: 'TextBlock', text: 'This is your new card inside another card',
    }]
  }
  const middle = {
    type: 'AdaptiveCard',
    body: [{ type: 'TextBlock', text: "This card's action will show another card" }],
    actions: [{ type: 'Action.ShowCard', title: 'Action.ShowCard', card: inner }],
  }
  const form = {
    type: 'AdaptiveCard',
    body: [{ type: 'Input.Text', id: 'name', label: 'Please enter your name:', isRequired: true, errorMessage: 'Name is required' }],
    actions: [{ type: 'Action.Execute', title: 'Submit', verb: 'submit_name', data: { action: 'submit_name' }, associatedInputs: 'auto' }],
  }
  return {
    type: 'AdaptiveCard',
    version: '1.5',
    body: [{ type: 'TextBlock', text: 'Adaptive Card Actions' }],
    actions: [
      { type: 'Action.OpenUrl', title: 'Action Open URL', url: 'https://adaptivecards.io' },
      { type: 'Action.ShowCard', title: 'Action Submit', card: form },
      { type: 'Action.ShowCard', title: 'Action ShowCard', card: middle },
    ],
  }
}

function visibilityCard () {
  return {
    type: 'AdaptiveCard',
    version: '1.5',
    body: [
      { type: 'TextBlock', text: 'Click to show or hide the message' },
      { type: 'TextBlock', id: 'helloWorld', text: '**Hello World!**', isVisible: false, size: 'ExtraLarge' },
    ],
    actions: [{ type: 'Action.ToggleVisibility', title: 'Click me!', targetElements: ['helloWorld'] }],
  }
}

export function createAgent () {
  const app = new AgentApplication({ storage: new MemoryStorage() })
  const teams = new TeamsAgentExtension(app)
  app.registerExtension(teams, () => {
    app.adaptiveCards.actionExecute<{ data?: { name?: string } }>('submit_name', async (context, _state, action) => {
      await context.sendActivity(`Data Submitted: ${action.data?.name ?? ''}`)
      return 'Action processed successfully'
    })
  })
  app.onActivity('message', async (context) => {
    const text = (context.activity.text ?? '').trim().toLowerCase()
    const card = text.includes('card actions') ? cardActions() : text.includes('toggle visibility') ? visibilityCard() : undefined
    if (card) {
      await context.sendActivity(Activity.fromObject({
        type: 'message',
        attachments: [{ contentType: 'application/vnd.microsoft.card.adaptive', content: card }],
      }))
    } else {
      await context.sendActivity("Welcome to the Cards Agent! To interact with me, send one of the following commands: 'card actions' or 'toggle visibility'")
    }
  })
  return app
}

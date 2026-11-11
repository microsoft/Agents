// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { startServer } from '@microsoft/agents-hosting-express'
import { TurnState, TurnContext, AgentApplication } from '@microsoft/agents-hosting'
import { Activity, ActivityTypes } from '@microsoft/agents-activity'
import { CardMessages } from './cardMessages'
import AdaptiveCard from './resources/adaptiveCard.json'

const cardsAgent = new AgentApplication<TurnState>()

cardsAgent.onConversationUpdate('membersAdded', async (context: TurnContext) => {
  const agentId = context.activity.recipient?.id
  if (context.activity.membersAdded?.some(member => member.id !== agentId)) {
    await CardMessages.sendIntroCard(context)
  }
})

cardsAgent.onActivity(ActivityTypes.Message, async (context: TurnContext) => {
  const input = context.activity.text?.trim().toLowerCase()

  if (input) {
    const command = input.match(/^([1-7])(?:\..*)?$/)?.[1] ?? input

    switch (command) {
      case 'display card options':
        await CardMessages.sendIntroCard(context)
        break
      case '1':
        await CardMessages.sendAdaptiveCard(context, AdaptiveCard)
        break
      case '2':
        await CardMessages.sendAnimationCard(context)
        break
      case '3':
        await CardMessages.sendAudioCard(context)
        break
      case '4':
        await CardMessages.sendHeroCard(context)
        break
      case '5':
        await CardMessages.sendReceiptCard(context)
        break
      case '6':
        await CardMessages.sendThumbnailCard(context)
        break
      case '7':
        await CardMessages.sendVideoCard(context)
        break
      default: {
        const reply: Activity = Activity.fromObject(
          {
            type: ActivityTypes.Message,
            text: 'Your input was not recognized, please try again.'
          }
        )
        await context.sendActivity(reply)
        await CardMessages.sendIntroCard(context)
      }
    }
  } else {
    await context.sendActivity('This sample is only for testing Cards using CardFactory methods. Please refer to other samples to test out more functionalities.')
  }
})

startServer(cardsAgent)

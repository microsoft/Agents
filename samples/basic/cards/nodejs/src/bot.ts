// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { ActivityHandler, Activity, ActivityTypes } from '@microsoft/agents-bot-hosting'
import { CardMessages } from './cardMessages'
import AdaptiveCard from './resources/adaptiveCard.json'

export class CardFactoryBot extends ActivityHandler {
  constructor() {
    super()

    this.onMembersAdded(async (context, next) => {
      const membersAdded = context.activity.membersAdded
      for (let cnt = 0; cnt < membersAdded!.length; cnt++) {
        if ((context.activity.recipient != null) && membersAdded![cnt].id !== context.activity.recipient.id) {
          await CardMessages.sendIntroCard(context)

          await next()
        }
      }
    })

    this.onMessage(async (context, next) => {
      if (context.activity.text !== undefined) {
        switch (context.activity.text.split('.')[0].toLowerCase()) {
          case 'display cards options':
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

      await next()
    })
  }
}

import { ActivityHandler, MessageFactory } from '@microsoft/agents-hosting'
import pjson from '@microsoft/agents-hosting/package.json'

export class EchoBot extends ActivityHandler {
  constructor () {
    super()
    this.onMessage(async (context, next) => {
      const text = context.activity.text
      const replyText = `Echo: ${text}`
      await context.sendActivity(MessageFactory.text(replyText, replyText))
      if (text?.includes('version')) {
        await context.sendActivity(MessageFactory.text('Running on version ' + pjson.version, 'Running on version ' + pjson.version))
      }
      await next()
    })

    this.onMembersAdded(async (context, next) => {
      const welcomeText = `Hello from echo bot, running on version ${pjson.version}`
      const membersAdded = context.activity.membersAdded
      if (membersAdded) {
        for (let cnt = 0; cnt < membersAdded.length; ++cnt) {
          if (membersAdded[cnt].id !== context.activity.recipient?.id) {
            await context.sendActivity(MessageFactory.text(welcomeText, welcomeText))
          }
        }
      }
      await next()
    })
  }
}

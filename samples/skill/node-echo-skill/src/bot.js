// @ts-check

import { ActivityHandler, MessageFactory } from '@microsoft/agents-bot-hosting'
import { default as pjson } from '../node_modules/@microsoft/agents-bot-hosting/package.json' with { type: "json" }

export class EchoBot extends ActivityHandler {
    constructor() {
        super()
        this.onMessage(async (context, next) => {
            const replyText = `Echo: ${ context.activity.text }`
            await context.sendActivity(MessageFactory.text(replyText, replyText))
            await next()
        });

        this.onMembersAdded(async (context, next) => {
            const welcomeText = `Hello from echo bot, running on version ${ pjson.version }`
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

// @ts-check

import { ActivityHandler, MessageFactory, MsalTokenProvider } from '@microsoft/agents-bot-hosting'
import { default as pjson } from '../node_modules/@microsoft/agents-bot-hosting/package.json' with { type: "json" }
import { getUserInfo } from './userGraphClient.js'

export class EchoBot extends ActivityHandler {
    constructor() {
        super()
        this.onMessage(async (context, next) => {
            const text = context.activity.text
            const replyText = `Echo: ${ text }`
            await context.sendActivity(MessageFactory.text(replyText, replyText))
            if (text?.includes('version')) {
                await context.sendActivity(MessageFactory.text('Running on version ' + pjson.version, 'Running on version ' + pjson.version))
            }
            if (text?.includes('graph')) {
                const msalClient = new MsalTokenProvider()
                const token = await msalClient.getAccessToken(context.adapter.authConfig, 'https://graph.microsoft.com/')
                const userInfo = await getUserInfo(token, context.activity.from?.aadObjectId)
                await context.sendActivity(MessageFactory.text(`User: ${ userInfo.$root.givenName } ${ userInfo.$root.surname } ${ userInfo.$root.mail }`))
                await context.sendActivity(MessageFactory.attachment({
                    contentType: 'image/png',
                    contentUrl: userInfo.$root.imageUri,
                    name: 'User photo'
                }))
            }
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

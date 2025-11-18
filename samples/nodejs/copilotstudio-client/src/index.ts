/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import * as msal from '@azure/msal-node'
import { Activity, ActivityTypes, CardAction } from '@microsoft/agents-activity'
import { ConnectionSettings, loadCopilotStudioConnectionSettingsFromEnv, CopilotStudioClient } from '@microsoft/agents-copilotstudio-client'
import pkg from '@microsoft/agents-copilotstudio-client/package.json' with { type: 'json' }
import readline from 'readline'
import open from 'open'
import os from 'os'
import path from 'path'

import { MsalCachePlugin } from './msalCachePlugin.js'

async function acquireToken (settings: ConnectionSettings): Promise<string> {
  const msalConfig = {
    auth: {
      clientId: settings.appClientId,
      authority: `https://login.microsoftonline.com/${settings.tenantId}`,
    },
    cache: {
      cachePlugin: new MsalCachePlugin(path.join(os.tmpdir(), 'mcssample.tockencache.json'))
    },
    system: {
      loggerOptions: {
        loggerCallback (loglevel: msal.LogLevel, message: string, containsPii: boolean) {
          if (!containsPii) {
            console.log(loglevel, message)
          }
        },
        piiLoggingEnabled: false,
        logLevel: msal.LogLevel.Verbose,
      }
    }
  }
  const pca = new msal.PublicClientApplication(msalConfig)
  const tokenRequest = {
    scopes: ['https://api.powerplatform.com/.default'],
    openBrowser: async (url: string) => {
      await open(url)
    }
  }
  let token
  try {
    const accounts = await pca.getAllAccounts()
    if (accounts.length > 0) {
      const response2 = await pca.acquireTokenSilent({ account: accounts[0], scopes: tokenRequest.scopes })
      token = response2.accessToken
    } else {
      const response = await pca.acquireTokenInteractive(tokenRequest)
      token = response.accessToken
    }
  } catch (error) {
    console.error('Error acquiring token interactively:', error)
    const response = await pca.acquireTokenInteractive(tokenRequest)
    token = response.accessToken
  }
  return token
}

const createClient = async (): Promise<CopilotStudioClient> => {
  const settings = loadCopilotStudioConnectionSettingsFromEnv()
  const token = await acquireToken(settings)
  const copilotClient = new CopilotStudioClient(settings, token)
  console.log(`Copilot Studio Client Version: ${pkg.version}, running with settings: ${JSON.stringify(settings, null, 2)}`)
  return copilotClient
}

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout
})

const askQuestion = async (copilotClient: CopilotStudioClient, conversationId: string) => {
  rl.question('\n>>>: ', async (answer) => {
    if (answer.toLowerCase() === 'exit') {
      rl.close()
      return
    } else if (answer.length > 0) {
      for await (const replyActivity of copilotClient.askQuestionAsync(answer, conversationId)) {
        if (replyActivity.type === ActivityTypes.EndOfConversation) {
          console.log(`\n${replyActivity.text}`)
          rl.close()
        } else {
          printActivity(replyActivity)
        }
      }
    }
    await askQuestion(copilotClient, conversationId)
  })
}

/**
 * Writes formatted data to the console. This funciton does not handle all of the possible activity types and formats,
 * it is focused on just a few common types.
 * @param act The activity to print.
 */
function printActivity (act: Activity): void {
  switch (act.type) {
    case ActivityTypes.Message: {
      if (act.textFormat === 'markdown') {
        console.log(`\n${act.text}`)
        act.suggestedActions?.actions?.forEach((action: CardAction) => console.log(`\t${action.value}`))
      } else {
        console.log(`\n${act.text}`)
      }
      break
    }
    case ActivityTypes.Typing: {
      console.log('\n...typing...')
      break
    }
    case ActivityTypes.Event: {
      console.log(`\n(event) ${act.name}`)
      break
    }
    default: console.log(`\n${act.type}`)
  }
}

const main = async () => {
  const copilotClient = await createClient()
  let conversationId = ''
  for await (const act of copilotClient.startConversationAsync(true)) {
    printActivity(act)
    conversationId = act.conversation?.id ?? ''
  }
  await askQuestion(copilotClient, conversationId)
}

main().catch(e => console.log(e))

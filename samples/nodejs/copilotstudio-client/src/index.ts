/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import * as msal from '@azure/msal-node'
import { Activity, ActivityTypes, CardAction } from '@microsoft/agents-activity'
import { ConnectionSettings, CopilotStudioClient } from '@microsoft/agents-copilotstudio-client'
import pkg from '@microsoft/agents-copilotstudio-client/package.json' with { type: 'json' }
import readline from 'readline'
import open from 'open'
import os from 'os'
import path from 'path'

import { MsalCachePlugin } from './msalCachePlugin.js'
import { SampleConnectionSettings } from './sampleConnectionSettings.js'

interface S2SConnectionSettings extends ConnectionSettings {
  appClientSecret?: string
}

async function acquireS2SToken (baseConfig: msal.Configuration, settings: S2SConnectionSettings): Promise<string> {
  const cca = new msal.ConfidentialClientApplication({
    ...baseConfig,
    auth: {
      ...baseConfig.auth,
      clientSecret: settings.appClientSecret!
    }
  })

  try {
    const response = await cca.acquireTokenByClientCredential({ scopes: [CopilotStudioClient.scopeFromSettings(settings)] })
    if (!response?.accessToken) {
      throw new Error('Failed to acquire token')
    }

    return response?.accessToken
  } catch (error) {
    console.error('Error acquiring token for client credential:', error)
    throw error
  }
}

async function acquireToken (baseConfig: msal.Configuration, settings: ConnectionSettings): Promise<string> {
  const tokenRequest = {
    scopes: [CopilotStudioClient.scopeFromSettings(settings)],
    openBrowser: async (url: string) => {
      await open(url)
    }
  }

  const pca = new msal.PublicClientApplication(baseConfig)

  try {
    const accounts = await pca.getAllAccounts()
    if (accounts.length > 0) {
      const response2 = await pca.acquireTokenSilent({ account: accounts[0], scopes: tokenRequest.scopes })
      return response2.accessToken
    } else {
      const response = await pca.acquireTokenInteractive(tokenRequest)
      return response.accessToken
    }
  } catch (error) {
    console.error('Error acquiring token interactively:', error)
    const response = await pca.acquireTokenInteractive(tokenRequest)
    return response.accessToken
  }
}

function getToken (settings: SampleConnectionSettings) : Promise<string> {
  const msalConfig: msal.Configuration = {
    auth: {
      clientId: settings.appClientId!,
      authority: `${settings.authority}/${settings.tenantId}`,
    },
    cache: {
      cachePlugin: new MsalCachePlugin(path.join(os.tmpdir(), 'msal.usercache.json'))
    },
    system: {
      loggerOptions: {
        loggerCallback (loglevel: msal.LogLevel, message: string, containsPii: boolean) {
          console.log(message)
        },
        piiLoggingEnabled: false,
        logLevel: msal.LogLevel.Verbose,
      }
    }
  }

  if (settings.useS2SConnection) {
    return acquireS2SToken(msalConfig, settings)
  }

  return acquireToken(msalConfig, settings)
}

const createClient = async (): Promise<CopilotStudioClient> => {
  const settings = new SampleConnectionSettings()
  const token = await getToken(settings)
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
      const replies = await copilotClient.askQuestionAsync(answer, conversationId)
      replies.forEach((act: Activity) => {
        if (act.type === ActivityTypes.Message) {
          console.log(`\n${act.text}`)
          act.suggestedActions?.actions.forEach((action: CardAction) => console.log(action.value))
        } else if (act.type === ActivityTypes.EndOfConversation) {
          console.log(`\n${act.text}`)
          rl.close()
        }
      })
    }
    await askQuestion(copilotClient, conversationId)
  })
}

const main = async () => {
  const copilotClient = await createClient()
  const act: Activity = await copilotClient.startConversationAsync(true)
  act.suggestedActions?.actions.forEach((action: CardAction) => console.log(action.value))
  await askQuestion(copilotClient, act.conversation?.id!)
}

main().catch(e => console.log(e))

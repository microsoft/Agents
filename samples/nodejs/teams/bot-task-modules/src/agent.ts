// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Activity } from '@microsoft/agents-activity'
import { AgentApplication, MemoryStorage } from '@microsoft/agents-hosting'
import { TeamsAgentExtension } from '@microsoft/agents-hosting-extensions-msteams'
import { emailCard, launcherCard, nameCard, textInputCard } from './cards.js'

export function createAgent (services: { endpoint?: string } = {}) {
  const endpoint = (services.endpoint ?? process.env.BOT_ENDPOINT ?? 'http://localhost:3978').replace(/\/$/, '')
  const app = new AgentApplication({ storage: new MemoryStorage() })
  const teams = new TeamsAgentExtension(app)
  app.registerExtension(teams, (extension) => {
    extension.taskModules.onFetch(null, async (_context, _state, request) => {
      const data = request.data as { data?: string } | undefined
      if (data?.data === 'CustomForm') {
        return {
          task: {
            type: 'continue',
            value: {
              title: 'Custom Form',
              width: 510,
              height: 450,
              url: `${endpoint}/CustomForm/`,
              fallbackUrl: `${endpoint}/CustomForm/`,
            }
          }
        }
      }
      if (data?.data === 'MultiStep') {
        return {
          task: {
            type: 'continue',
            value: {
              title: 'Multi-step Form', width: 400, height: 300, card: nameCard(),
            }
          }
        }
      }
      return {
        task: {
          type: 'continue',
          value: {
            title: 'Adaptive Card: Inputs', width: 400, height: 200, card: textInputCard(),
          }
        }
      }
    })
    extension.taskModules.onSubmit(null, async (context, _state, request) => {
      const data = (request.data ?? {}) as { submissiontype?: string; name?: string; email?: string; usertext?: string }
      if (data.submissiontype === 'multi_step_1') {
        return {
          task: {
            type: 'continue',
            value: {
              title: 'Multi-step Form: Step 2', width: 400, height: 300, card: emailCard(data.name ?? ''),
            }
          }
        }
      }
      if (data.submissiontype === 'multi_step_2' || data.submissiontype === 'custom_form') {
        await context.sendActivity(`Hi ${data.name}, thanks for submitting! Your email is ${data.email}`)
        const message = data.submissiontype === 'multi_step_2' ? 'Multi-step form completed!' : 'Form submitted successfully'
        return { task: { type: 'message', value: message } }
      }
      await context.sendActivity(`You submitted: ${data.usertext}`)
      return { task: { type: 'message', value: 'Thanks for submitting!' } }
    })
  })
  app.onActivity('message', async (context) => {
    await context.sendActivity(Activity.fromObject({ type: 'message', attachments: [launcherCard()] }))
  })
  return app
}

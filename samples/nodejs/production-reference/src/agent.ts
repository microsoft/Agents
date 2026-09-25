// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { AgentApplication, CloudAdapter, loadAuthConfigFromEnv, type Storage, type TurnContext, type TurnState } from '@microsoft/agents-hosting'
import type { AppConfig } from './config.js'
import { advanceIssueCapture, initialIssueCaptureState, type SupportIssueCaptureState } from './issue-capture.js'
import { recordFailure, recordTurn, runIssueCaptureTurn } from './telemetry.js'

const issueCaptureKey = 'conversation.supportIssueCapture'

export function createAgent (config: AppConfig, storage: Storage): AgentApplication<TurnState> {
  const adapter = new CloudAdapter(
    loadAuthConfigFromEnv(),
    undefined,
    undefined,
    { validateServiceUrl: config.isProduction, emitStackTrace: false }
  )
  const agent = new AgentApplication<TurnState>({ adapter, storage })

  // Returning true from afterTurn lets the SDK save changed conversation state.
  agent.onTurn('afterTurn', async () => true)

  agent.onConversationUpdate('membersAdded', async (context: TurnContext) => {
    const userJoined = context.activity.membersAdded?.some((member) => member.id !== context.activity.recipient?.id)
    if (userJoined) {
      await context.sendActivity('Welcome. Describe the support issue you want to report, for example: "Checkout is unavailable."')
    }
  })

  agent.onActivity('message', async (context: TurnContext, state: TurnState) => runIssueCaptureTurn(async () => {
    const current = state.getValue<SupportIssueCaptureState>(issueCaptureKey) ?? initialIssueCaptureState()
    const text = context.activity.text ?? ''
    const result = advanceIssueCapture(current, text)
    state.setValue(issueCaptureKey, result.state)
    recordTurn()
    await context.sendActivity(result.reply)
  }))

  agent.onError(async (context, _error) => {
    recordFailure('turn')
    // Do not send implementation details, request content, or credentials to the user.
    await context.sendActivity('Sorry, the request could not be processed. Please try again later.')
  })

  return agent
}

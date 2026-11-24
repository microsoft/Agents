// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import test from 'node:test'
import { Activity } from '@microsoft/agents-activity'
import { CloudAdapter, MemoryStorage, TurnContext } from '@microsoft/agents-hosting'
import { createAgent } from '../src/agent.js'

test('support issue capture continues across message turns', async () => {
  const agent = createAgent({ isProduction: false, port: 3978, blobContainerId: 'test' }, new MemoryStorage())
  const adapter = new CloudAdapter()

  async function sendMessage (text: string): Promise<string[]> {
    const context = new TurnContext(adapter, Activity.fromObject({
      type: 'message',
      id: `activity-${text}`,
      channelId: 'webchat',
      serviceUrl: 'https://webchat.botframework.com/',
      from: { id: 'user-id' },
      recipient: { id: 'agent-id' },
      conversation: { id: 'conversation-id' },
      text,
    }))
    const replies: string[] = []
    context.onSendActivities(async (_context, activities) => {
      replies.push(...activities.map((activity) => activity.text ?? ''))
      return activities.map((_activity, index) => ({ id: `reply-${index}` }))
    })
    await agent.run(context)
    return replies
  }

  assert.deepEqual(await sendMessage('Checkout is unavailable'), ['What is the impact: low, medium, or high?'])
  assert.deepEqual(await sendMessage('medium'), ['Support issue capture saved to this conversation with medium impact.'])
})

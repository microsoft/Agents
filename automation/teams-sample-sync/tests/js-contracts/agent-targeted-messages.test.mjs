import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { createRequire } from 'node:module'
import path from 'node:path'
import { sampleHarness } from './harness.mjs'

const group = {
  id: 'chat-1;messageid=123',
  isGroup: true,
  conversationType: 'groupChat'
}
const slash = {
  id: 'agent-1',
  isTargeted: true
}

async function reminderHarness (extra = {}) {
  const timers = []
  const harness = await sampleHarness({
    now: () => 1000,
    schedule: (run, delay) => {
      const timer = {
        run,
        delay,
        cancelled: false
      }
      timers.push(timer)

      return () => {
        timer.cancelled = true
      }
    },
    ...extra,
  })

  return {
    ...harness,
    timers
  }
}

function assertPrivate (activity, recipient, incomingId) {
  assert.equal(activity.recipient.id, recipient)
  assert.ok(activity.entities.some((entity) => entity.type === 'activityTreatment' && entity.treatment === 'targeted'))
  if (incomingId) {
    assert.ok(activity.entities.some((entity) => entity.type === 'targetedMessageInfo' && entity.targetedMessageId === incomingId))
  }
}

function reminderId (response) {
  return response.attachments[0].content.actions[0].data.reminderId
}

function action (verb, data) {
  return {
    type: 'invoke',
    name: 'adaptiveCard/action',
    conversation: group,
    value: {
      action: {
        type: 'Action.Execute',
        verb,
        data: {
          action: verb,
          ...data
        }
      }
    }
  }
}

describe('agent-targeted-messages', () => {
  it('reminders preserve mentions, private confirmation and delayed recipient delivery through the SDK', async () => {
    const harness = await reminderHarness()
    const result = await harness.turn({
      conversation: group,
      recipient: slash,
      text: '<at>Agent</at> remind <at>Bob</at> in 2 mins to review PR',
      entities: [
        {
          type: 'mention',
          text: '<at>Agent</at>',
          mentioned: { id: 'agent-1' }
        },
        {
          type: 'mention',
          text: '<at>Bob</at>',
          mentioned: {
            id: 'user-2',
            name: 'Bob'
          }
        },
      ],
    })
    const response = result.sent[0]
    assertPrivate(response, 'user-1', 'incoming-activity')
    assert.equal(response.conversation.id, group.id)
    assert.equal(response.attachments[0].content.body[1].facts[0].value, 'review PR')
    assert.equal(harness.timers[0].delay, 120_000)
    harness.sent.length = 0
    await harness.timers[0].run()
    assert.equal(harness.continued[0].identity.aud, 'test-agent-app')
    assert.equal(harness.continued[0].reference.conversation.id, 'chat-1')
    assert.equal(harness.continued[0].reference.serviceUrl, 'https://example.invalid')
    assertPrivate(harness.sent[0], 'user-2')
    assert.equal(harness.sent[0].attachments[0].content.body[1].text, 'review PR')
    assert.deepEqual(harness.sent[0].attachments[0].content.actions.map((item) => item.verb), ['dismiss_reminder', 'snooze_reminder'])
    const after = await harness.turn({
      text: 'my-reminders',
      conversation: group
    })
    assert.equal(after.sent[0].text, 'You have no active reminders.')
  })

  it('lists filter unrelated users and cancellation enforces creator/recipient ownership', async () => {
    const harness = await reminderHarness()
    const created = await harness.turn({
      text: 'remind me in 1 hour that meeting starts',
      conversation: group
    })
    const id = reminderId(created.sent[0])
    assert.equal(harness.timers[0].delay, 3_600_000)
    const list = await harness.turn({
      text: 'my-reminders',
      conversation: group,
      recipient: slash
    })
    assertPrivate(list.sent[0], 'user-1', 'incoming-activity')
    assert.match(list.sent[0].text, /meeting starts.*1h 0m/)
    const unrelated = {
      id: 'user-3',
      name: 'Charlie'
    }
    const hidden = await harness.turn({
      text: 'my-reminders',
      conversation: group,
      from: unrelated
    })
    assert.equal(hidden.sent[0].text, 'You have no active reminders.')
    const denied = await harness.turn({
      text: `cancel-reminder ${id}`,
      conversation: group,
      from: unrelated
    })
    assert.match(denied.sent[0].text, /only cancel reminders you created/)
    assert.equal(harness.timers[0].cancelled, false)
    const cancelled = await harness.turn({
      text: `cancel-reminder ${id}`,
      conversation: group
    })
    assert.match(cancelled.sent[0].text, /has been cancelled/)
    assert.equal(harness.timers[0].cancelled, true)
    harness.sent.length = 0
    await harness.timers[0].run()
    assert.equal(harness.sent.length, 0)
  })

  it('card cancel, dismiss and snooze produce valid invoke responses and cancellable timers', async () => {
    const harness = await reminderHarness()
    const created = await harness.turn({
      text: 'remind me in 30 seconds test',
      conversation: group
    })
    const id = reminderId(created.sent[0])
    const cancelled = await harness.turn(action('cancel_reminder', { reminderId: id }))
    assert.equal(cancelled.invoke.value.body.value, 'Reminder cancelled!')
    assert.equal(harness.timers[0].cancelled, true)
    const missing = await harness.turn(action('cancel_reminder', { reminderId: id }))
    assert.match(missing.invoke.value.body.value, /not found/)
    const snooze = await harness.turn(action('snooze_reminder', {
      reminderText: 'test',
      snoozeMinutes: '5'
    }))
    assert.equal(snooze.invoke.value.body.value, 'Snoozed for 5 minutes!')
    const confirmation = snooze.sent.find((item) => item.attachments)
    assertPrivate(confirmation, 'user-1')
    assert.equal(harness.timers[1].delay, 300_000)
    const dismissed = await harness.turn(action('dismiss_reminder', { reminderId: reminderId(confirmation) }))
    assert.equal(dismissed.invoke.value.body.value, 'Reminder dismissed!')
    assert.equal(harness.timers[1].cancelled, true)
  })

  it('help and fallback preserve suggested actions, AI label and targeted prompt preview', async () => {
    const harness = await reminderHarness()
    const help = await harness.turn({
      text: 'reminder-help',
      conversation: group
    })
    assertPrivate(help.sent[0], 'user-1')
    assert.match(help.sent[0].text, /remind @John.*my-reminders.*add-reaction/s)
    assert.equal(help.sent[0].suggestedActions.actions[0].value.command, 'remind me in 30 seconds test')
    const ordinary = await harness.turn({
      text: 'hello',
      conversation: group
    })
    assert.ok(ordinary.sent[0].entities.some((entity) => entity.additionalType?.includes('AIGeneratedContent')))
    assert.equal(ordinary.sent[0].recipient.isTargeted, undefined)
    const privateReply = await harness.turn({
      text: 'hello',
      conversation: group,
      recipient: slash
    })
    assertPrivate(privateReply.sent[0], 'user-1', 'incoming-activity')
  })

  it('suggested-action invokes share reminder/help/list routing and acknowledge the invoke', async () => {
    const harness = await reminderHarness()
    const invoke = (command) => harness.turn({
      type: 'invoke',
      name: 'suggestedActions/submit',
      conversation: group,
      value: { command }
    })
    for (const command of ['help', 'remind me in 1s test', 'my-reminders', 'custom-command', undefined]) {
      const result = await invoke(command)
      assert.equal(result.invoke.value.status, 200)
      assert.ok(result.sent.length)
      if (command === 'custom-command') {
        assert.equal(result.sent[0].text, 'Executing: custom-command')
      }
      if (!command) {
        assert.equal(result.sent[0].text, 'No command specified.')
      }
    }
    assert.equal(harness.timers[0].delay, 1000)
  })

  it('reaction commands use quoted message then reply then current ID and reject slash commands', async () => {
    const calls = []
    const harness = await reminderHarness({
      reaction: async (_context, ...args) => {
        calls.push(args)
      }
    })
    await harness.turn({
      text: '<quoted messageId="quoted-1"/> add-reaction like',
      replyToId: 'reply-1'
    })
    await harness.turn({
      text: 'remove-reaction heart',
      replyToId: 'reply-1'
    })
    await harness.turn({ text: 'add-reaction launch' })
    assert.deepEqual(calls, [['add', 'quoted-1', 'like'], ['delete', 'reply-1', 'heart'], ['add', 'incoming-activity', 'launch']])
    const blocked = await harness.turn({
      text: 'add-reaction like',
      recipient: slash,
      conversation: group
    })
    assert.match(blocked.sent[0].text, /cannot be added via slash/)
    assertPrivate(blocked.sent[0], 'user-1', 'incoming-activity')
    assert.equal(calls.length, 3)
    const invalid = await harness.turn({ text: 'remove-reaction' })
    assert.match(invalid.sent[0].text, /specify a reaction type/)
  })

  it('default reaction transport wraps the routed context and calls the Teams client', async () => {
    const require = createRequire(path.resolve(process.argv[2], 'package.json'))
    const { TeamsTurnContext } = require('@microsoft/agents-hosting-extensions-msteams')
    const harness = await reminderHarness()
    const calls = []
    harness.app.onTurn('beforeTurn', async (context) => {
      const client = new TeamsTurnContext(context).client
      client.reactions.add = async (...args) => {
        calls.push(['add', ...args])
      }
      client.reactions.delete = async (...args) => {
        calls.push(['delete', ...args])
      }

      return true
    })
    const added = await harness.turn({ text: 'add-reaction like' })
    const removed = await harness.turn({ text: 'remove-reaction like' })
    assert.match(added.sent[0].text, /Added a/)
    assert.match(removed.sent[0].text, /Removed the/)
    assert.deepEqual(calls, [
      ['add', 'conversation-1', 'incoming-activity', 'like'],
      ['delete', 'conversation-1', 'incoming-activity', 'like'],
    ])
  })

  it('reaction events acknowledge additions and accept removals without an extra reply', async () => {
    const harness = await reminderHarness()
    const added = await harness.turn({
      type: 'messageReaction',
      from: { id: 'user-2' },
      reactionsAdded: [{
        type: 'heart',
        user: { displayName: 'Bob' }
      }, {
        type: 'like',
        user: { displayName: 'Carol' }
      }],
    })
    assert.equal(added.sent[0].text, 'Thanks for the **heart** reaction, Bob!')
    assert.equal(added.sent[1].text, 'Thanks for the **like** reaction, Carol!')
    const removed = await harness.turn({
      type: 'messageReaction',
      reactionsRemoved: [{ type: 'heart' }]
    })
    assert.equal(removed.sent.length, 0)
  })

  it('invalid time and timer overflow fail without scheduling, and factory state stays isolated', async () => {
    const harness = await reminderHarness()
    for (const command of ['remind me tomorrow', 'remind me in 999999 hours', 'remind me in 0s']) {
      const result = await harness.turn({ text: command })
      assert.match(result.sent[0].text, /Could not parse|positive reminder/)
    }
    assert.equal(harness.timers.length, 0)
    await harness.turn({ text: 'remind me in 30secs' })
    const other = await reminderHarness()
    assert.equal((await other.turn({ text: 'my-reminders' })).sent[0].text, 'You have no active reminders.')
  })
})

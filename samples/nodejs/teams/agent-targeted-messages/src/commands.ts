// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Activity } from '@microsoft/agents-activity'
import { Conversation, type AgentApplication, type TurnContext, type TurnState } from '@microsoft/agents-hosting'
import { confirmation, delivery, helpText, suggested, targeted } from './messages.js'
import { formatTimeSpan, parseReminder, ReminderStore, type Reminder } from './reminders.js'

export class ReminderCommands {
  constructor (
    readonly store: ReminderStore,
    private readonly app: AgentApplication<TurnState>
  ) {}

  create (context: TurnContext, input: Omit<Reminder, 'id' | 'dueTime'>, delayMs: number) {
    // Keep a serializable reference and adapter, never the revocable turn context.
    const conversation = new Conversation(context)
    conversation.reference = structuredClone(conversation.reference)
    conversation.reference.conversation.id = conversation.reference.conversation.id.split(';')[0]
    const adapter = context.adapter
    return this.store.create(input, delayMs, async (reminder) => {
      await this.app.proactive.continueConversation(adapter, conversation, async (continued) => {
        const message = targeted(delivery(reminder), continued.activity, {
          id: reminder.targetUserId, name: reminder.targetUserName, role: 'user',
        })
        await continued.sendActivity(message)
      })
    })
  }

  async remind (context: TurnContext, command: string) {
    const parsed = parseReminder(context.activity, command)
    if ('error' in parsed) {
      await context.sendActivity(targeted(Activity.fromObject({ type: 'message', text: `${parsed.error}\n\nUse \`reminder-help\` for usage examples.` }), context.activity))
      return
    }
    const reminder = this.create(context, {
      targetUserId: parsed.targetUserId,
      targetUserName: parsed.targetUserName,
      reminderText: parsed.reminderText,
      creatorId: context.activity.from?.id ?? '',
      creatorName: context.activity.from?.name ?? 'Someone',
    }, parsed.delayMs)
    try {
      await context.sendActivity(targeted(confirmation(reminder, parsed.delayMs), context.activity))
    } catch (error) {
      this.store.cancel(reminder.id, reminder.creatorId)
      throw error
    }
  }

  async list (context: TurnContext) {
    const userId = context.activity.from?.id ?? ''
    const reminders = this.store.forUser(userId)
    const lines = reminders.map((reminder) => {
      const remaining = reminder.dueTime - this.store.now()
      const when = remaining > 0 ? `in ${formatTimeSpan(remaining)}` : 'overdue'
      const target = reminder.creatorId === reminder.targetUserId ? 'yourself' : reminder.targetUserName
      return `- **${reminder.id}**: "${reminder.reminderText}" for ${target} (${when})`
    })
    const text = lines.length ? `**Your Active Reminders:**\n\n${lines.join('\n')}\n\nUse \`cancel-reminder [id]\` to cancel a reminder.` : 'You have no active reminders.'
    const response = targeted(Activity.fromObject({ type: 'message', text }), context.activity)
    if (!lines.length) {
      suggested(response, userId, [
        ['Remind me in 5 minutes test', 'remind me in 5 minutes test'],
        ['Remind me in 1 hour meeting', 'remind me in 1 hour meeting'],
        ['Show help', 'reminder-help'],
      ])
    }
    await context.sendActivity(response)
  }

  async cancel (context: TurnContext, id: string) {
    const result = id ? this.store.cancel(id, context.activity.from?.id ?? '') : 'missing'
    let text = `Reminder **${id}** not found or already completed.`
    if (!id) {
      text = 'Please specify a reminder ID. Use `my-reminders` to see your active reminders.'
    } else if (result === 'cancelled') {
      text = `Reminder **${id}** has been cancelled.`
    } else if (result === 'denied') {
      text = 'You can only cancel reminders you created or are assigned to you.'
    }
    await context.sendActivity(targeted(Activity.fromObject({ type: 'message', text }), context.activity))
  }

  async help (context: TurnContext) {
    await context.sendActivity(targeted(suggested(Activity.fromObject({ type: 'message', text: helpText }), context.activity.from?.id ?? '', [
      ['Set a 30-second test reminder', 'remind me in 30 seconds test'],
      ['Set a 5-minute reminder', 'remind me in 5 minutes check email'],
      ['My reminders', 'my-reminders'],
    ]), context.activity))
  }
}

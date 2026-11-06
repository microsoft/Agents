// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Activity } from '@microsoft/agents-activity'
import { AgentApplication, MemoryStorage, RouteRank } from '@microsoft/agents-hosting'
import { TeamsAgentExtension, TeamsTurnContext } from '@microsoft/agents-hosting-extensions-msteams'
import { ReminderCommands } from './commands.js'
import { isTargeted, reactionUserName, snoozed, suggested, targeted } from './messages.js'
import { ReminderStore, type ReminderServices } from './reminders.js'
import { reactionCommand, sendReaction, type ReactionService } from './reactions.js'
export { normalizeIncoming } from './messages.js'

interface CardData {
  action?: string
  reminderId?: string
  reminderText?: string
  snoozeMinutes?: string
}

export function createAgent (services: ReminderServices & { reaction?: ReactionService } = {}) {
  const storage = new MemoryStorage()
  const app = new AgentApplication({ storage, proactive: { storage } })
  const teams = new TeamsAgentExtension(app)
  const commands = new ReminderCommands(new ReminderStore(services), app)
  app.registerExtension(teams, () => {})

  for (const verb of ['cancel_reminder', 'dismiss_reminder', 'snooze_reminder']) {
    app.adaptiveCards.actionExecute<{ data?: CardData }>(verb, async (context, _state, action) => {
      const data = action.data
      if (!data) {
        return 'No data specified.'
      }
      const id = data.reminderId ?? ''
      const userId = context.activity.from?.id ?? ''
      if (verb === 'cancel_reminder' || verb === 'dismiss_reminder') {
        const result = commands.store.cancel(id, userId)
        if (result === 'denied') {
          return 'You can only cancel reminders you created or are assigned to you.'
        }
        if (verb === 'dismiss_reminder') {
          return 'Reminder dismissed!'
        }
        return result === 'cancelled' ? 'Reminder cancelled!' : 'Reminder not found or already completed.'
      }
      const minutes = Number.parseInt(data.snoozeMinutes || '5', 10) || 5
      if (minutes < 1 || minutes * 60_000 > 2_147_483_647) {
        return 'Invalid snooze duration.'
      }
      const reminder = commands.create(context, {
        creatorId: userId,
        creatorName: context.activity.from?.name ?? 'User',
        targetUserId: userId,
        targetUserName: context.activity.from?.name ?? 'User',
        reminderText: data.reminderText || 'Snoozed reminder',
      }, minutes * 60_000)
      try {
        await context.sendActivity(targeted(snoozed(reminder, minutes), context.activity))
      } catch (error) {
        commands.store.cancel(reminder.id, userId)
        throw error
      }
      return `Snoozed for ${minutes} minutes!`
    })
  }

  app.onActivity('message', async (context) => {
    if (!context.activity.text) {
      return
    }
    let text = context.activity.text.trim()
    for (const entity of context.activity.entities ?? []) {
      const mention = entity as { type: string; mentioned?: { id: string }; text?: string }
      if (mention.type === 'mention' && mention.mentioned?.id === context.activity.recipient?.id && mention.text) {
        text = text.replace(mention.text, '').trim()
      }
    }
    const quote = /<quoted\s+messageId="([^"]+)"\s*\/>/i.exec(text)
    if (quote) {
      text = text.replace(quote[0], '').trim()
    }
    const lower = text.toLowerCase()
    if (lower === 'help' || lower === 'reminder-help') {
      await commands.help(context)
    } else if (lower.startsWith('remind')) {
      await commands.remind(context, text)
    } else if (lower === 'my-reminders') {
      await commands.list(context)
    } else if (lower.startsWith('cancel-reminder')) {
      await commands.cancel(context, text.replace(/^cancel-reminder\s*/i, '').trim())
    } else if (/^(add|remove)-reaction/i.test(lower)) {
      await reactionCommand(new TeamsTurnContext(context), text, quote?.[1], services.reaction ?? sendReaction)
    } else {
      const response = suggested(Activity.fromObject({
        type: 'message',
        text: 'Use `reminder-help` to see available commands.',
        entities: [{ type: 'https://schema.org/Message', '@type': 'Message', '@context': 'https://schema.org', additionalType: ['AIGeneratedContent'] }],
      }), context.activity.from?.id ?? '', [
        ['Show help', 'reminder-help'],
        ['Remind me in 5 minutes test', 'remind me in 5 minutes test'],
        ['My reminders', 'my-reminders'],
      ])
      await context.sendActivity(isTargeted(context.activity) ? targeted(response, context.activity) : response)
    }
  })

  app.onActivity('messageReaction', async (context) => {
    for (const [index, reaction] of (context.activity.reactionsAdded ?? []).entries()) {
      const user = (reaction as { user?: { displayName?: string } }).user
      const name = user?.displayName || reactionUserName(context.activity, index) || context.activity.from?.name || 'Someone'
      await context.sendActivity(`Thanks for the **${reaction.type}** reaction, ${name}!`)
    }
    for (const reaction of context.activity.reactionsRemoved ?? []) {
      console.log(`Reaction removed: ${reaction.type}`)
    }
  })
  app.addRoute((context) => Promise.resolve(context.activity.type === 'invoke' && context.activity.name === 'suggestedActions/submit'), async (context) => {
    const command = (context.activity.value as { command?: string } | undefined)?.command
    if (!command) {
      await context.sendActivity('No command specified.')
    } else if (['help', 'reminder-help'].includes(command.toLowerCase())) {
      await commands.help(context)
    } else if (/^remind/i.test(command)) {
      await commands.remind(context, command)
    } else if (command.toLowerCase() === 'my-reminders') {
      await commands.list(context)
    } else {
      await context.sendActivity(`Executing: ${command}`)
    }
    await context.sendActivity(Activity.fromObject({ type: 'invokeResponse', value: { status: 200 } }))
  }, true, RouteRank.First)
  return app
}

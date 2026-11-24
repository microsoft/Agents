// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { randomUUID } from 'node:crypto'
import type { Activity } from '@microsoft/agents-activity'

export interface Reminder {
  id: string
  targetUserId: string
  targetUserName: string
  creatorId: string
  creatorName: string
  reminderText: string
  dueTime: number
}

const timePattern = /in\s+(\d+)\s*(seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h)\b/i

export function parseReminder (activity: Activity, command: string) {
  let text = command.replace(/^remind\s*/i, '').trim()
  let target = { id: activity.from?.id ?? '', name: activity.from?.name ?? 'You' }
  if (/^me(\s|,|$)/i.test(text)) {
    text = text.slice(2).trim().replace(/^,/, '').trim()
  } else {
    for (const entity of activity.entities ?? []) {
      const mention = entity as { type: string; mentioned?: { id: string; name?: string }; text?: string }
      if (mention.type === 'mention' && mention.mentioned && mention.mentioned.id !== activity.recipient?.id) {
        target = { id: mention.mentioned.id, name: mention.mentioned.name ?? 'User' }
        text = text.replace(mention.text ?? '', '').trim()
        break
      }
    }
  }
  const match = timePattern.exec(text)
  if (!match) {
    return { error: "Could not parse time. Use format like 'in 5 minutes', 'in 1 hour', or 'in 30 seconds'." }
  }
  const unit = match[2].toLowerCase()
  const delayMs = Number(match[1]) * (unit.startsWith('s') ? 1000 : unit.startsWith('m') ? 60_000 : 3_600_000)
  // Node timers overflow above this bound and would otherwise fire immediately.
  if (!Number.isSafeInteger(delayMs) || delayMs < 1 || delayMs > 2_147_483_647) {
    return { error: 'Choose a positive reminder delay of at most 24 days.' }
  }
  if (!target.id) {
    return { error: 'Could not determine who to remind. Use `remind me` or mention someone.' }
  }
  const reminderText = text.replace(timePattern, '').trim().replace(/^,\s*/, '').replace(/^(to|that)\s+/i, '') || 'You have a reminder!'
  return { targetUserId: target.id, targetUserName: target.name, reminderText, delayMs }
}

export function formatTimeSpan (milliseconds: number) {
  const seconds = Math.round(milliseconds / 1000)
  if (seconds >= 3600) {
    return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`
  }
  if (seconds >= 60) {
    return `${Math.floor(seconds / 60)}m ${seconds % 60}s`
  }
  return `${seconds}s`
}

export interface ReminderServices {
  now?: () => number
  schedule?: (callback: () => Promise<void>, milliseconds: number) => () => void
}

/** Each agent factory owns its own store and timers; nothing is shared between instances. */
export class ReminderStore {
  private readonly entries = new Map<string, { reminder: Reminder; cancel: () => void }>()
  readonly now: () => number
  private readonly schedule: NonNullable<ReminderServices['schedule']>

  constructor (services: ReminderServices) {
    this.now = services.now ?? Date.now
    this.schedule = services.schedule ?? ((callback, milliseconds) => {
      const timer = setTimeout(() => {
        callback().catch((error) => console.error('Reminder timer failed:', error))
      }, milliseconds)
      timer.unref()
      return () => clearTimeout(timer)
    })
  }

  create (input: Omit<Reminder, 'id' | 'dueTime'>, delayMs: number, deliver: (reminder: Reminder) => Promise<void>) {
    const reminder = { ...input, id: randomUUID(), dueTime: this.now() + delayMs }
    const cancel = this.schedule(async () => {
      if (!this.entries.has(reminder.id)) {
        return
      }
      this.entries.delete(reminder.id)
      try {
        await deliver(reminder)
      } catch (error) {
        console.error('Reminder delivery failed:', error)
      }
    }, delayMs)
    this.entries.set(reminder.id, { reminder, cancel })
    return reminder
  }

  forUser (userId: string) {
    return [...this.entries.values()].map(({ reminder }) => reminder)
      .filter((reminder) => reminder.creatorId === userId || reminder.targetUserId === userId)
      .sort((left, right) => left.dueTime - right.dueTime)
  }

  cancel (id: string, userId: string): 'cancelled' | 'missing' | 'denied' {
    const entry = this.entries.get(id)
    if (!entry) {
      return 'missing'
    }
    if (entry.reminder.creatorId !== userId && entry.reminder.targetUserId !== userId) {
      return 'denied'
    }
    entry.cancel()
    this.entries.delete(id)
    return 'cancelled'
  }
}

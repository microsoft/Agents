// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Activity } from '@microsoft/agents-activity'
import { formatTimeSpan, type Reminder } from './reminders.js'

export function isTargeted (activity: Activity) {
  return activity.isTargetedActivity() || !!(activity.recipient as { isTargeted?: boolean } | undefined)?.isTargeted
}

/** Preserve Teams' slash flag before SDK nested account parsing discards extra fields. */
export function normalizeIncoming (activity: {
  recipient?: { isTargeted?: boolean }
  reactionsAdded?: Array<{ user?: { displayName?: string } }>
  entities?: Array<{ type: string; [key: string]: unknown }>
}) {
  if (activity.recipient?.isTargeted && !activity.entities?.some((entity) =>
    entity.type === 'activityTreatment' && entity.treatment === 'targeted')) {
    activity.entities = [...(activity.entities ?? []), { type: 'activityTreatment', treatment: 'targeted' }]
  }
  const names = activity.reactionsAdded?.map((reaction) => reaction.user?.displayName ?? '')
  if (names?.some(Boolean) && !activity.entities?.some((entity) => entity.type === 'reminderReactionUsers')) {
    activity.entities = [...(activity.entities ?? []), { type: 'reminderReactionUsers', names }]
  }
  return activity
}

export function reactionUserName (activity: Activity, index: number) {
  const entity = activity.entities?.find((item) => item.type === 'reminderReactionUsers') as { names?: string[] } | undefined
  return entity?.names?.[index]
}

export function targeted (activity: Activity, source: Activity, user = source.from) {
  activity.conversation = source.conversation
  const recipient = { ...user, isTargeted: true }
  activity.recipient = recipient
  if (source.conversation?.isGroup) {
    activity.makeTargetedActivity()
  }
  if (isTargeted(source) && source.id) {
    activity.entities = [...(activity.entities ?? []), { type: 'targetedMessageInfo', targetedMessageId: source.id }]
  }
  return activity
}

export function suggested (activity: Activity, userId: string, commands: Array<[string, string]>) {
  activity.suggestedActions = {
    to: userId ? [userId] : [],
    actions: commands.map(([title, command]) => ({ type: 'Action.Submit', title, value: { command } })),
  }
  return activity
}

function execute (title: string, action: string, data: object) {
  return { type: 'Action.Execute', title, verb: action, data: { action, ...data } }
}

function card (body: object[], actions: object[]) {
  return Activity.fromObject({
    type: 'message',
    attachments: [{
      contentType: 'application/vnd.microsoft.card.adaptive',
      content: {
        type: 'AdaptiveCard', version: '1.5', body, actions,
      }
    }],
  })
}

export function confirmation (reminder: Reminder, delayMs: number) {
  return card([
    { type: 'TextBlock', text: 'Reminder Set!', weight: 'Bolder', size: 'Medium', color: 'Good' },
    {
      type: 'FactSet',
      facts: [
        { title: 'Reminder:', value: reminder.reminderText },
        { title: 'For:', value: reminder.creatorId === reminder.targetUserId ? 'yourself' : reminder.targetUserName },
        { title: 'In:', value: formatTimeSpan(delayMs) },
        { title: 'ID:', value: reminder.id },
      ]
    },
    { type: 'TextBlock', text: 'This is a targeted message — only you can see this.', size: 'Small', isSubtle: true, wrap: true },
  ], [execute('Cancel Reminder', 'cancel_reminder', { reminderId: reminder.id })])
}

export function delivery (reminder: Reminder) {
  return card([
    { type: 'TextBlock', text: 'Reminder', weight: 'Bolder', size: 'Large', color: 'Accent' },
    { type: 'TextBlock', text: reminder.reminderText, wrap: true, size: 'Medium' },
    { type: 'TextBlock', text: `Set by ${reminder.creatorId === reminder.targetUserId ? 'yourself' : reminder.creatorName}`, size: 'Small', isSubtle: true },
  ], [
    execute('Dismiss', 'dismiss_reminder', { reminderId: reminder.id }),
    execute('Snooze 5 min', 'snooze_reminder', { reminderId: reminder.id, reminderText: reminder.reminderText, snoozeMinutes: '5' }),
  ])
}

export function snoozed (reminder: Reminder, minutes: number) {
  const activity = card([
    { type: 'TextBlock', text: 'Snoozed!', weight: 'Bolder', size: 'Medium', color: 'Accent' },
    { type: 'TextBlock', text: reminder.reminderText, wrap: true },
    { type: 'TextBlock', text: `Will remind you again in ${minutes} minutes.`, size: 'Small', isSubtle: true },
  ], [execute('Cancel', 'cancel_reminder', { reminderId: reminder.id })])
  activity.text = `Snoozed for ${minutes} minutes`
  return activity
}

export const helpText = [
  '**Personal Reminder Agent - Help**',
  '',
  '**Set a Reminder:**',
  '- `remind me in 5 minutes to check email`',
  '- `remind me in 1 hour meeting starts`',
  '- `remind me in 30 seconds test`',
  '- `remind @John in 10 minutes review PR`',
  '',
  '**Supported Time Formats:** seconds/secs/s, minutes/mins/m, hours/hrs/h.',
  '**Manage Reminders:** `my-reminders`, `cancel-reminder [id]`, `reminder-help`.',
  'Reminders are targeted to one recipient in channels and group chats. Use card buttons to dismiss or snooze.',
  '**Reactions:** `add-reaction [type]`, `remove-reaction [type]` in a regular @mention message.',
  'Supported types include `like`, `heart`, `1f440_eyes`, `2705_whiteheavycheckmark`, `launch`, `1f4cc_pushpin`.',
].join('\n')

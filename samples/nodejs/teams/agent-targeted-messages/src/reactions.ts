// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
import { Activity } from '@microsoft/agents-activity'
import type { TeamsTurnContext } from '@microsoft/agents-hosting-extensions-msteams'
import { isTargeted, targeted } from './messages.js'

type Reaction = Parameters<TeamsTurnContext['client']['reactions']['add']>[2]

export interface ReactionService {
  (context: TeamsTurnContext, operation: 'add' | 'delete', messageId: string, reaction: string): Promise<unknown>
}

export const sendReaction: ReactionService = async (context, operation, messageId, reaction) => {
  return context.client.reactions[operation](context.activity.conversation?.id ?? '', messageId, reaction as Reaction)
}

export async function reactionCommand (
  context: TeamsTurnContext,
  command: string,
  quotedMessageId: string | undefined,
  service: ReactionService
) {
  const remove = /^remove-reaction/i.test(command)
  const reaction = command.replace(/^(add|remove)-reaction\s*/i, '').trim()
  if (!reaction) {
    await context.sendActivity(`Please specify a reaction type. Example: \`${remove ? 'remove' : 'add'}-reaction like\``)
    return
  }
  if (isTargeted(context.activity)) {
    const response = Activity.fromObject({
      type: 'message',
      text: `Reactions cannot be ${remove ? 'removed' : 'added'} via slash commands. Please use \`${remove ? 'remove' : 'add'}-reaction\` by @mentioning the bot in a regular message.`,
    })
    await context.sendActivity(targeted(response, context.activity))
    return
  }
  try {
    const messageId = quotedMessageId || context.activity.replyToId || context.activity.id || ''
    await service(context, remove ? 'delete' : 'add', messageId, reaction)
    await context.sendActivity(remove ? `Removed the **${reaction}** reaction from your message!` : `Added a **${reaction}** reaction to your message!`)
  } catch (error) {
    console.error('Reaction request failed:', error)
    await context.sendActivity(`Sorry, I had trouble ${remove ? 'removing' : 'adding'} that reaction.`)
  }
}

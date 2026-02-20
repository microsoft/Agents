/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import { BaseAdapter, CloudAdapter, TurnContext } from '@microsoft/agents-hosting'
import { ActivityTypes, Activity, ConversationReference } from '@microsoft/agents-activity'
import * as readline from 'readline'

 // Implements the CloudAdapter interface.
export class ConsoleAdapter extends CloudAdapter {
  nextId: number
  reference: ConversationReference
  constructor (reference?: ConversationReference) {
    super()
    this.nextId = 0
    this.reference = {
      ...reference,
      channelId: 'console',
      user: { id: 'user', name: 'User1' },
      agent: { id: 'bot', name: 'Bot' },
      conversation: { id: 'convo1', name: '', isGroup: false },
      serviceUrl: ''
    }
  }

  // Listens for incoming messages from the console.
  listen (logic: { (context: TurnContext): Promise<void>; (revocableContext: TurnContext): Promise<void>; }) {
    const rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout,
      terminal: false
    } )
    rl.on('line', async line => {
      // Initialize activity
      const activity = Activity.fromObject({ type: ActivityTypes.Message, text: line })
      activity.applyConversationReference(
        this.reference,
        true
      )

      // Create context and run middleware pipe
      const context = new TurnContext(this as unknown as BaseAdapter, activity)
      await this.runMiddleware(context, logic).catch(err => {
        this.printError(err.toString())
      })
    })
    return () => {
      rl.close()
    }
  }

  continueConversation (reference: ConversationReference, logic: (revocableContext: TurnContext) => Promise<void>) {
    const activity = new Activity(ActivityTypes.Message)
    activity.applyConversationReference(
      reference,
      true
    )
    const context = new TurnContext(this as unknown as BaseAdapter, activity)
    return this.runMiddleware(context, logic).catch(err => {
      this.printError(err.toString())
    })
  }

  async sendActivities (_context: TurnContext, activities: Activity[]) {
    const responses = []
    for (const activity of activities) {
      // Generate a unique id for each activity response
      const id = (activity.id || `console-${Date.now()}-${Math.random().toString(36).substring(2, 11)}`)
      responses.push({ id })
      this.print(activity.text || '')
    }
    return responses
  }

  updateActivity (_context: TurnContext, _activity: Activity) {
    return Promise.reject(new Error('ConsoleAdapter.updateActivity(): not supported.'))
  }

  deleteActivity (_context: TurnContext, _reference: Partial<ConversationReference>) {
    return Promise.reject(new Error('ConsoleAdapter.deleteActivity(): not supported.'))
  }

  print (line: string) {
    console.log(line)
  }

  printError (line: string) {
    console.error(line)
  }
}

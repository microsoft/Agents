/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import { BaseAdapter, CloudAdapter, TurnContext } from '@microsoft/agents-hosting'
import { ActivityTypes, Activity, ConversationReference } from '@microsoft/agents-activity'
import * as readline from 'readline'

/**
 * Lets a user communicate with a bot from a console window.
 *
 * @remarks
 * The following example shows the typical adapter setup:
 *
 * ```typescript
 * import { ConsoleAdapter } from './consoleAdapter';
 *
 * const adapter = new ConsoleAdapter();
 * const closeFn = adapter.listen(async (context) => {
 *    await context.sendActivity(`Hello World`);
 * });
 * ```
 *
 * This adapter is intended for local development and testing scenarios.
 * It implements the CloudAdapter interface for compatibility with the Agents SDK.
 */
export class ConsoleAdapter extends CloudAdapter {
  nextId: number
  reference: ConversationReference
  /**
   * Creates a new ConsoleAdapter instance.
   * @param [reference] Reference used to customize the address information of activities sent from the adapter.
   */
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

  /**
   * Begins listening to console input. Returns a function that can be called to stop listening.
   *
   * @remarks
   * When input is received from the console:
   * - A 'message' activity is created with the user's input text.
   * - A revocable `TurnContext` is created for the activity.
   * - The context is routed through any middleware registered with [use()](#use).
   * - The bot's logic handler is executed.
   * - The middleware promise chain is resolved.
   * - The context object is revoked, and future calls to its members will throw a `TypeError`.
   *
   * ```typescript
   * const closeFn = adapter.listen(async (context) => {
   *    const utterance = context.activity.text.toLowerCase();
   *    if (utterance.includes('goodbye')) {
   *       await context.sendActivity(`Ok... Goodbye`);
   *       closeFn();
   *    } else {
   *       await context.sendActivity(`Hello World`);
   *    }
   * });
   * ```
   * @param logic Function called each time a message is input by the user.
   * @returns Function to stop listening to console input.
   */
  listen (logic: { (context: TurnContext): Promise<void>; (revocableContext: TurnContext): Promise<void>; }) {
    const rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout,
      terminal: false
    })
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

  /**
   * Allows the bot to proactively message the user.
   *
   * @remarks
   * The processing steps for this method are similar to [listen()](#listen):
   * - A `TurnContext` is created and routed through the adapter's middleware.
   * - The provided logic handler is executed.
   * The main difference is that since no activity was received from the user, a new activity is created and populated with the provided conversation reference.
   * The created activity will have its address-related fields populated, and `context.activity.type` will be set to `message`.
   *
   * ```typescript
   * function delayedNotify(context, message, delay) {
   *    const reference = TurnContext.getConversationReference(context.activity);
   *    setTimeout(() => {
   *       adapter.continueConversation(reference, async (ctx) => {
   *          await ctx.sendActivity(message);
   *       });
   *    }, delay);
   * }
   * ```
   * @param reference A `ConversationReference` saved during a previous message from a user. This can be calculated for any incoming activity using `TurnContext.getConversationReference(context.activity)`.
   * @param logic A function handler that will be called to perform the bot's logic after the adapter's middleware has been run.
   */
  continueConversation (reference: ConversationReference, logic: (revocableContext: TurnContext) => Promise<void>) {
    // Create context and run middleware pipe
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

  /**
   * Sends a set of activities to the console.
   *
   * @remarks
   * This method is called by `TurnContext.sendActivities()` or `TurnContext.sendActivity()` to output activities to the console.
   * It ensures outgoing activities have been properly addressed, and prints them appropriately.
   * Middleware and addressing are handled by the Agents SDK before this method is called.
   * @param _context Context for the current turn of conversation with the user.
   * @param activities List of activities to send.
   */
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

  /**
   * Not supported for the ConsoleAdapter.  Calling this method or `TurnContext.updateActivity()`
   * will result an error being returned.
   */
  updateActivity (_context: TurnContext, _activity: Activity) {
    return Promise.reject(new Error('ConsoleAdapter.updateActivity(): not supported.'))
  }

  /**
   * Not supported for the ConsoleAdapter.  Calling this method or `TurnContext.deleteActivity()`
   * will result an error being returned.
   */
  deleteActivity (_context: TurnContext, _reference: Partial<ConversationReference>) {
    return Promise.reject(new Error('ConsoleAdapter.deleteActivity(): not supported.'))
  }

  /**
   * Logs text to the console.
   * @param line Text to print.
   */
  print (line: string) {
    console.log(line)
  }

  /**
   * Logs an error to the console.
   * @param line Error text to print.
   */
  printError (line: string) {
    console.error(line)
  }
}

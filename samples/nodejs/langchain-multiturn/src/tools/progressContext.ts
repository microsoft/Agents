// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { AsyncLocalStorage } from 'node:async_hooks'
import { TurnContext } from '@microsoft/agents-hosting'

const turnContextStorage = new AsyncLocalStorage<TurnContext>()

export function runWithTurnContext<T> (context: TurnContext, callback: () => Promise<T>): Promise<T> {
  return turnContextStorage.run(context, callback)
}

export async function reportProgress (message: string): Promise<void> {
  console.log(message)
  const context = turnContextStorage.getStore()
  if (context) {
    await context.streamingResponse.queueInformativeUpdate(message)
  }
}

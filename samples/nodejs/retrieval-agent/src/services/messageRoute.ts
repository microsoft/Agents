// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { createSourceCard } from './card.js'
import type { RetrievalResult, RetrievalStatus } from './retrievalClient.js'

export const buildGenieResponses: Record<Exclude<RetrievalStatus, 'success'>, string> = {
  notSignedIn: 'Please sign in to Microsoft 365, then ask your Build question again.',
  noResults: "I couldn't find Build session information in the configured SharePoint site. Check the site URL, document permissions, and indexing, then try a more specific question.",
  serviceUnavailable: "I couldn't retrieve Build session information right now. Please try again later."
}

export function groundedAnswer (result: RetrievalResult): string {
  return `Here is what I found in the configured SharePoint site:\n\n${result.items.map(item => `${item.title}\n${item.extract}\nSource: ${item.webUrl}`).join('\n\n')}`
}

export async function handleBuildGenieMessage (
  result: RetrievalResult,
  sendText: (text: string) => Promise<unknown>,
  sendSourceCard: (card: object) => Promise<unknown>
): Promise<void> {
  if (result.status !== 'success') {
    await sendText(buildGenieResponses[result.status])
    return
  }

  await sendText(groundedAnswer(result))
  await sendSourceCard(createSourceCard(result.items))
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import type { RetrievalItem } from './retrievalClient.js'

export function createSourceCard (items: RetrievalItem[]): object {
  return {
    type: 'AdaptiveCard',
    $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
    version: '1.5',
    body: items.map(item => ({
      type: 'Container',
      items: [
        { type: 'TextBlock', text: item.title, weight: 'Bolder', wrap: true },
        { type: 'TextBlock', text: item.extract, wrap: true, spacing: 'Small' }
      ],
      selectAction: { type: 'Action.OpenUrl', title: 'Open source', url: item.webUrl }
    }))
  }
}

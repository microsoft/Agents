// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { z } from 'zod'
import { defineTool } from '@github/copilot-sdk'

const inventories = new Map<string, string[]>()

export function createInventoryTool(conversationId: string) {
  return defineTool('manage_inventory', {
    description: "Manage the adventurer's inventory - add, remove, or list items",
    parameters: z.object({
      action: z.enum(['add', 'remove', 'list']).describe("Action to perform"),
      item: z.string().optional().describe('Item name (required for add/remove)'),
    }),
    handler: async ({ action, item }) => {
      if (!inventories.has(conversationId)) {
        inventories.set(conversationId, [])
      }
      const inventory = inventories.get(conversationId)!

      switch (action) {
        case 'add':
          if (!item) return 'Specify an item to add to the inventory'
          inventory.push(item)
          return `📦 Added '${item}' to inventory. (${inventory.length} items total)`

        case 'remove': {
          if (!item) return 'Specify an item to remove from the inventory'
          const idx = inventory.indexOf(item)
          if (idx !== -1) {
            inventory.splice(idx, 1)
            return `🗑️ Removed '${item}' from inventory. (${inventory.length} items remaining)`
          }
          return `'${item}' not found in inventory`
        }

        case 'list': {
          if (inventory.length === 0) return "🎒 The adventurer's pack is empty"
          const items = inventory.map((entry) => `  • ${entry}`).join('\n')
          return `🎒 Inventory (${inventory.length} items):\n${items}`
        }

        default:
          return `Unknown action '${action}'. Use 'add', 'remove', or 'list'`
      }
    },
  })
}
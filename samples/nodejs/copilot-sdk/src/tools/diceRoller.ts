// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { z } from 'zod'
import { defineTool } from '@github/copilot-sdk'

export function createRollDiceTool () {
  return defineTool('roll_dice', {
    description: 'Roll dice using standard notation (e.g., 2d6+3, 1d20, 4d8-1)',
    parameters: z.object({
      notation: z.string().describe("Dice notation like '2d6+3', '1d20', '4d8-1'"),
    }),
    handler: async ({ notation }) => {
      const trimmed = notation.trim().toLowerCase()
      const match = trimmed.match(/^(\d+)d(\d+)([+-]\d+)?$/)

      if (!match) {
        return `Invalid dice notation: ${notation}. Use format like 2d6+3`
      }

      const numDice = parseInt(match[1], 10)
      const numSides = parseInt(match[2], 10)
      const modifier = match[3] ? parseInt(match[3], 10) : 0

      if (numDice < 1 || numDice > 100 || numSides < 2 || numSides > 1000) {
        return 'Please use reasonable dice values (1-100 dice, 2-1000 sides)'
      }

      const rolls = Array.from({ length: numDice }, () => Math.floor(Math.random() * numSides) + 1)
      const total = rolls.reduce((a, b) => a + b, 0) + modifier

      const modStr = modifier > 0 ? ` + ${modifier}` : modifier < 0 ? ` - ${Math.abs(modifier)}` : ''
      return `🎲 Rolling ${notation}: [${rolls.join(', ')}]${modStr} = ${total}`
    },
  })
}

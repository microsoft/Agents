// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Preserve useful validator errors across the Copilot custom-tool boundary. */
import type { ToolResultObject } from '@github/copilot-sdk'
import { diagnosticText } from './agent-runner.js'

export async function runValidationTool<T> (
  validate: () => Promise<T>,
  report: (message: string) => void
): Promise<T | ToolResultObject> {
  try {
    return await validate()
  } catch (error) {
    // The SDK otherwise replaces thrown handler errors with "Tool execution failed".
    const message = diagnosticText(error instanceof Error ? error.message : String(error))
    report(message)

    return {
      resultType: 'failure',
      textResultForLlm: message,
      error: message,
    }
  }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { runValidationTool } from '../../src/validation-tool.js'

describe('validation-tool', () => {
  it('validation errors retain actionable details without exposing credentials', async () => {
    const messages: string[] = []
    const result = await runValidationTool(async () => {
      throw new Error('Cannot fingerprint installed package: WinError 3; token ghp_examplecredential')
    }, (message) => messages.push(message))

    assert.deepEqual(result, {
      resultType: 'failure',
      textResultForLlm: 'Cannot fingerprint installed package: WinError 3; token [REDACTED]',
      error: 'Cannot fingerprint installed package: WinError 3; token [REDACTED]',
    })
    assert.deepEqual(messages, [result.error])
  })

  it('failed candidate checks remain structured validation evidence', async () => {
    const evidence = {
      passed: false,
      checks: {
        contracts: {
          status: 'failed',
          errors: ['Wrong invoke response']
        }
      }
    }
    const result = await runValidationTool(async () => evidence, () => assert.fail('Unexpected infrastructure error'))

    assert.equal(result, evidence)
  })
})

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { isolateAuthenticationFailure, withMigrationCredentialFallback, type MigrationAttempt } from '../../src/migration-auth.js'

const rejected: MigrationAttempt = {
  code: 1,
  planningCompleted: false,
  error: 'get_model_list: {"status":403,"body":"Organization does not have the Allow use of Copilot CLI billed to the organization policy enabled"}',
}

describe('migration-auth', () => {
  it('retry cannot see the previous failure and retains both attempts after completion', async () => {
    const root = mkdtempSync(path.join(tmpdir(), 'auth-retry-test-'))
    const output = path.join(root, 'output')
    mkdirSync(output)
    writeFileSync(path.join(output, 'sync-result.json'), 'primary failure')
    writeFileSync(path.join(output, 'agent-log.txt'), 'primary log')

    try {
      const code = await withMigrationCredentialFallback('automatic', 'pat', async (token) => {
        if (token === 'automatic') {
          return rejected
        }

        assert.equal(existsSync(output), false)
        mkdirSync(output)
        writeFileSync(path.join(output, 'sync-result.json'), 'retry success')

        return {
          code: 0,
          planningCompleted: true
        }
      }, () => {}, () => isolateAuthenticationFailure(output))
      assert.equal(code, 0)
      assert.equal(readFileSync(path.join(output, 'sync-result.json'), 'utf8'), 'retry success')
      assert.equal(readFileSync(path.join(output, 'authentication-attempt/sync-result.json'), 'utf8'), 'primary failure')
      assert.equal(readFileSync(path.join(output, 'authentication-attempt/agent-log.txt'), 'utf8'), 'primary log')
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('organization billing rejection retries once with the PAT', async () => {
    const tokens: Array<string | undefined> = []
    const code = await withMigrationCredentialFallback('automatic', 'pat', async (token) => {
      tokens.push(token)

      return token === 'automatic'
        ? rejected
        : {
            code: 0,
            planningCompleted: true
          }
    }, () => {})
    assert.equal(code, 0)
    assert.deepEqual(tokens, ['automatic', 'pat'])
  })

  it('success, unrelated failures, and failures after planning never switch credentials', async () => {
    for (const result of [
      {
        code: 0,
        planningCompleted: true
      },
      {
        ...rejected,
        planningCompleted: true
      },
      {
        ...rejected,
        error: 'get_model_list: {"status":401}'
      },
      {
        ...rejected,
        error: 'get_model_list: {"status":403,"body":"Access denied"}'
      },
      {
        ...rejected,
        error: 'Validation failed'
      },
    ]) {
      const tokens: Array<string | undefined> = []
      assert.equal(await withMigrationCredentialFallback('automatic', 'pat', async (token) => {
        tokens.push(token)

        return result
      }), result.code)
      assert.deepEqual(tokens, ['automatic'])
    }
  })

  it('missing PAT and rejected PAT preserve failure without further retries', async () => {
    for (const fallback of [undefined, 'pat']) {
      const tokens: Array<string | undefined> = []
      assert.equal(await withMigrationCredentialFallback('automatic', fallback, async (token) => {
        tokens.push(token)

        return rejected
      }, () => {}), 1)
      assert.deepEqual(tokens, fallback ? ['automatic', 'pat'] : ['automatic'])
    }
  })
})

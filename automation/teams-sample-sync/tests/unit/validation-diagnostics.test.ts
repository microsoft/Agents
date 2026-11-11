import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { runMigrationSession } from '../../src/sync-session.js'
import { assertFullValidation } from '../../src/validate.js'
import { FakeSession, validation } from '../fixtures/migration-session.js'

describe('validation-diagnostics', () => {
  it('reports the failing contract after the repair pass is exhausted', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-diagnostics-'))
    const session = new FakeSession([
      '## Migration plan\n- Preserve file consent',
      '## Self-audit\nOutcome: changed\nSummary: Updated file consent.',
      '## Self-audit\nOutcome: changed\nSummary: Repaired file consent.',
    ])
    const result = validation(true, 'current')
    const failure = 'IConnectorClient must be registered in ITurnContext.Services and implement IRestTransport.'
    result.passed = false
    result.checks.contracts = {
      status: 'failed',
      errors: [failure]
    }
    result.errors = [failure]
    let validationRuns = 0
    try {
      await assert.rejects(runMigrationSession({
        sample: result.sample,
        contextFile: 'context.json',
        output,
        session,
        validate: async () => {
          validationRuns += 1

          return result
        },
      }), { message: `Full validation failed for dotnet/sample-a:\ncontracts (failed): ${failure}`, })
      assert.equal(validationRuns, 2)
      assert.equal(session.phases.at(-1), 'Repairing validation failures')
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('still rejects partial or mismatched validation evidence', () => {
    const result = validation(true, 'current')
    result.group = 'code'
    assert.throws(() => assertFullValidation(result, result.sample), /Current full validation is required/)
    result.group = 'all'
    assert.throws(() => assertFullValidation(result, 'another-sample'), /Current full validation is required/)
  })

  it('retains validation-level errors when no check provides the cause', () => {
    const result = validation(true, 'current')
    result.passed = false
    result.errors = ['Required validation check did not pass: contracts']
    result.checks.contracts = {
      status: 'skipped',
      errors: []
    }
    assert.throws(() => assertFullValidation(result, result.sample), { message: 'Full validation failed for dotnet/sample-a:\nRequired validation check did not pass: contracts', })
  })
})

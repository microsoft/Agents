import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { runMigrationSession } from '../../src/sync-session.js'
import { FakeSession, validation } from '../fixtures/migration-session.js'

describe('migration outcome reconciliation', () => {
  it('reconciles a claimed edit with an empty sample diff before reporting success', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Update README if needed',
      '## Self-audit\nOutcome: changed\nUpdated README',
      '## Self-audit\nOutcome: no changes required\nREADME already matches',
    ])
    let validationRuns = 0
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => {
          validationRuns += 1

          return validation(true, `pass-${validationRuns}`)
        },
        sampleChanges: () => [],
      })
      assert.equal(result.outcome, 'no-changes')
      assert.equal(result.repairPasses, 1)
      assert.equal(validationRuns, 2)
      assert.equal(session.phases[2], 'Reconciling migration outcome')
      assert.match(session.prompts[2]!, /actual changed files are: \[\]/)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('fails after one outcome retry if the claimed edit still has no sample diff', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Update README',
      '## Self-audit\nOutcome: changed\nUpdated README',
      '## Self-audit\nOutcome: changed\nUpdated README',
    ])
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session,
          validate: async () => validation(true, 'ready'),
          sampleChanges: () => [],
        }),
        /Implementer outcome "changed" disagrees with selected-sample changed files: \[\]/
      )
      assert.equal(session.phases.filter((phase) => phase === 'Reconciling migration outcome').length, 1)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('accepts a missing edit made during outcome reconciliation after revalidation', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Update README',
      '## Self-audit\nOutcome: changed\nUpdated README',
      '## Self-audit\nOutcome: changed\nSummary: Clarified the README setup steps.\nREADME now updated',
    ])
    let checks = 0
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => {
          checks += 1

          return validation(true, `pass-${checks}`)
        },
        sampleChanges: () =>
          session.phases.includes('Reconciling migration outcome') ? ['samples/dotnet/teams/sample-a/README.md'] : [],
      })
      assert.equal(result.outcome, 'changed')
      assert.equal(result.summary, 'Clarified the README setup steps.')
      assert.equal(result.repairPasses, 1)
      assert.equal(checks, 2)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('shows the base diff when reconciling an unreported edit', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Correct README domain',
      '## Self-audit\nOutcome: no changes required\nREADME already matches',
      '## Self-audit\nOutcome: changed\nSummary: Corrected the documented link-unfurling domain.',
    ])
    const readme = 'samples/dotnet/teams/sample-a/README.md'
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
        sampleChanges: () => [readme],
        sampleDiff: () => '- Link-unfurling domain: `*.wikipedia.org`\n+ Link-unfurling domain: `en.wikipedia.org`',
      })
      assert.equal(result.outcome, 'changed')
      assert.match(session.prompts[2]!, /- Link-unfurling domain: `\*\.wikipedia\.org`/)
      assert.match(session.prompts[2]!, /\+ Link-unfurling domain: `en\.wikipedia\.org`/)
      assert.match(session.prompts[2]!, /retain it and report "Outcome: changed"/)
      assert.match(session.prompts[2]!, /restore the exact base content/)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })
})

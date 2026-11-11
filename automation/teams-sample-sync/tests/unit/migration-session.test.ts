import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { assertOutcomeMatchesSampleChanges, runMigrationSession } from '../../src/sync-session.js'
import { FakeSession, validation } from '../fixtures/migration-session.js'

describe('migration session', () => {
  it('requests a concrete summary with write access disabled when the audit omits it', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Update the card action',
      '## Self-audit\nOutcome: changed\n- Card action updated',
      'Summary: Added the new card action and matching manifest command.',
    ])
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
      })
      assert.equal(result.summary, 'Added the new card action and matching manifest command.')
      assert.equal(session.phases[2], 'Summarizing verified migration')
      assert.equal(session.writableStates[2], false)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('accepts a long Markdown summary without shortening it', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const longSummary = 'Removed redundant storage registration and added example launch profiles; verified the meeting event handlers, manifest, configuration, and documentation against the Agents SDK migration plan. The sample also passed project, restore, build, manifest, HTTP smoke, and contract checks.'
    const session = new FakeSession([
      '## Migration plan\n- Update meeting setup',
      `## Self-audit\nOutcome: changed\n**Summary:** ${longSummary}`,
    ])
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
      })
      assert.equal(result.summary, longSummary)
      assert.equal(session.phases.length, 2)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('uses a safe fallback when the summary-only retry is malformed', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Update meeting setup',
      '## Self-audit\nOutcome: changed\n- Meeting setup updated',
      'Here is the completed migration summary without the requested label.',
    ])
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
      })
      assert.equal(result.summary, 'Updated sample-a and passed automated validation.')
      assert.equal(session.phases[2], 'Summarizing verified migration')
      assert.equal(session.writableStates[2], false)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('freezes the plan before implementation and repairs validation once', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan\n- Program.Main',
      '## Self-audit\nOutcome: changed\n- Program.Main done',
      '## Self-audit\nOutcome: changed\nSummary: Fixed the Program.Main sample flow.\n- Program.Main fixed',
    ])
    const results = [validation(false, 'first'), validation(true, 'second')]
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => results.shift()!,
      })
      assert.equal(result.repairPasses, 1)
      assert.equal(session.writable, true)
      assert.equal(session.prompts.length, 3)
      assert.deepEqual(session.phases, [
        'Planning migration',
        'Implementing frozen migration plan',
        'Repairing validation failures',
      ])
      assert.match(session.prompts[0]!, /actual manifest JSON[\s\S]*bots\[\]\.commandLists/)
      assert.match(
        session.prompts[0]!,
        /minimumAgentsSdkVersion is a floor[\s\S]*selectedAgentsSdkVersion is the resolved target/
      )
      assert.match(session.prompts[0]!, /proceed autonomously; do not ask for confirmation/i)
      assert.match(session.prompts[1]!, /frozen migration plan/)
      assert.match(
        session.prompts[1]!,
        /Use selectedAgentsSdkVersion[\s\S]*minimumAgentsSdkVersion is only a lower bound/
      )
      assert.match(session.prompts[1]!, /schema validity alone is insufficient/)
      assert.match(session.prompts[1]!, /proceed autonomously; do not ask for confirmation/i)
      assert.match(session.prompts[2]!, /Build failed/)
      assert.match(session.prompts[2]!, /do not explain away a semantic mismatch/)
      assert.equal(readFileSync(path.join(output, 'migration-plan.md'), 'utf8'), '## Migration plan\n- Program.Main\n')
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('requires a self-audit after a successful implementation', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    let validationRan = false
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session: new FakeSession([
            '## Migration plan',
            "I'll begin the implementation now.",
            'Still implementing.',
            'Still no self-audit.',
          ]),
          validate: async () => {
            validationRan = true

            return validation(true, 'ready')
          },
        }),
        /missing required heading "## Self-audit"; the next full validation pass was not run\. Response ended with: "Still no self-audit\."/
      )
      assert.equal(validationRan, false)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('explains a missing self-audit outcome and previews the end of the response', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const response = '## Self-audit\nAudit without an outcome.'
    const retryResponse = `## Self-audit\n${'Earlier audit detail. '.repeat(30)}The destination sample was already fully implemented.`
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session: new FakeSession([
            '## Migration plan',
            response,
            '## Self-audit\nStill missing an outcome.',
            retryResponse,
          ]),
          validate: async () => validation(true, 'ready'),
        }),
        (error: Error) => {
          assert.match(
            error.message,
            /missing required line "Outcome: changed" or "Outcome: no changes required"; the next full validation pass was not run\./
          )
          assert.match(
            error.message,
            /Response ended with: "….*The destination sample was already fully implemented\."/
          )
          assert.ok(error.message.length < retryResponse.length)

          return true
        }
      )
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('accurately reports a malformed self-audit after a failed validation pass', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    let validationRuns = 0
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session: new FakeSession([
            '## Migration plan',
            '## Self-audit\nOutcome: changed',
            'Repair finished.',
            'Still repairing.',
            'Still missing the repair audit.',
          ]),
          validate: async () => {
            validationRuns += 1

            return validation(false, 'failed')
          },
        }),
        /missing required heading "## Self-audit"; the next full validation pass was not run\. Response ended with: "Still missing the repair audit\."/
      )
      assert.equal(validationRuns, 1)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects progress-only plans and contradictory outcomes', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session: new FakeSession([
            "I'll begin by inspecting the sample.",
            'May I proceed with the full planning analysis?',
            'Still inspecting.',
          ]),
          validate: async () => validation(true, 'ready'),
        }),
        /missing required heading "## Migration plan".*Response ended with: "Still inspecting\."/
      )
      assert.throws(() => assertOutcomeMatchesSampleChanges('changed', []), /selected sample is unchanged/)
      assert.throws(
        () => assertOutcomeMatchesSampleChanges('no-changes', ['samples/dotnet/teams/sample/Program.cs']),
        /selected sample was modified/
      )
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('continues an unfinished plan autonomously instead of waiting for confirmation', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      'I need to inspect the sample. May I proceed?',
      '## Migration plan\n- No changes expected',
      '## Self-audit\nOutcome: no changes required\n- Plan verified',
    ])
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
      })

      assert.equal(result.outcome, 'no-changes')
      assert.deepEqual(session.phases, [
        'Planning migration',
        'Continuing migration planning',
        'Implementing frozen migration plan',
      ])
      assert.deepEqual(session.writableStates, [false, false, true])
      assert.equal(session.writable, true)
      assert.match(session.prompts[1]!, /continue autonomously/i)
      assert.match(session.prompts[1]!, /do not ask for confirmation/i)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('formats completed continuation responses only after autonomous recovery', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      "I'll begin by inspecting the sample.",
      'Analysis complete; no changes are needed.',
      '## Migration plan\n- No changes expected',
      'I need to finish checking the implementation.',
      'Implementation and validation are complete.',
      '## Self-audit\nOutcome: no changes required\n- Plan verified',
    ])
    try {
      const result = await runMigrationSession({
        sample: 'sample-a',
        contextFile: '.sync/context/sync-context.json',
        output,
        session,
        validate: async () => validation(true, 'ready'),
      })

      assert.equal(result.outcome, 'no-changes')
      assert.deepEqual(session.phases, [
        'Planning migration',
        'Continuing migration planning',
        'Repairing migration plan format',
        'Implementing frozen migration plan',
        'Continuing migration implementation',
        'Repairing self-audit format',
      ])
      assert.deepEqual(session.writableStates, [false, false, false, true, true, false])
      assert.match(session.prompts[2]!, /format only/i)
      assert.match(session.prompts[5]!, /format only/i)
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })

  it('keeps write access disabled when a self-audit format retry fails', async () => {
    const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-session-'))
    const session = new FakeSession([
      '## Migration plan',
      'Implementation response without an audit.',
      'Implementation still incomplete.',
      new Error('Format retry failed'),
    ])
    try {
      await assert.rejects(
        runMigrationSession({
          sample: 'sample-a',
          contextFile: '.sync/context/sync-context.json',
          output,
          session,
          validate: async () => validation(true, 'ready'),
        }),
        /Format retry failed/
      )
      assert.equal(session.writable, false)
      assert.deepEqual(session.writableStates, [false, true, true, false])
    } finally {
      rmSync(output, {
        recursive: true,
        force: true
      })
    }
  })
})

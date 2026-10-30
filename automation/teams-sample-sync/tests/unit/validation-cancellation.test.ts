import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { it } from 'node:test'
import { runMigrationSession } from '../../src/sync-session.js'
import { cancelValidationProcesses, commandErrors } from '../../src/validate.js'
import type { ValidationResult } from '../../src/types.js'

it('cancels validation children when the migration deadline expires', async () => {
  const output = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-cancellation-'))
  const marker = path.join(output, 'late-output.txt')
  let command: Promise<string[]> | undefined
  let aborted = false
  const answers = ['## Migration plan\n- Validate the sample', '## Self-audit\nOutcome: changed\n- Updated sample']
  try {
    await assert.rejects(runMigrationSession({
      sample: 'sample-a',
      contextFile: 'context.json',
      output,
      deadlineMs: 100,
      session: {
        send: async () => answers.shift() ?? '',
        setWriteAccess: () => {},
        close: async () => {},
        abort: async () => { aborted = true },
      },
      validate: async () => {
        command = commandErrors(process.execPath, [
          '-e',
          "setTimeout(() => require('node:fs').writeFileSync(process.argv[1], 'late'), 1500)",
          marker,
        ], output)
        await command
        return {} as ValidationResult
      },
    }), /migration deadline exceeded/)
    assert.equal(aborted, true)
    assert.ok(command)
    await assert.rejects(command, /cancelled/)
    assert.equal(existsSync(marker), false)
    assert.deepEqual(await commandErrors(process.execPath, ['-e', 'process.exit(0)'], output), [])
  } finally {
    cancelValidationProcesses()
    rmSync(output, { recursive: true, force: true })
  }
})

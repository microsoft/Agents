import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { prepareHandoffBundle } from '../../src/handoff-bundle.js'
import type { SyncResult } from '../../src/types.js'

describe('Copilot handoff bundle', () => {
  it('includes ordered sample patches and evidence without generated PR metadata', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-handoff-'))
    const resultsDirectory = path.join(root, 'results')
    const output = path.join(root, 'handoff')
    const samples = ['bot-cards', 'bot-meetings']
    const results: SyncResult[] = []

    try {
      const planFile = path.join(root, 'plan.json')
      writeFileSync(planFile, '{"version":3}\n')

      for (const sample of samples) {
        const directory = path.join(resultsDirectory, `teams-sample-sync-${sample}`)
        mkdirSync(directory, { recursive: true })
        for (const name of [
          'change.patch',
          'sync-result.json',
          'migration-plan.md',
          'self-audit.md',
          'source-context.json',
        ]) {
          writeFileSync(path.join(directory, name), `${sample}: ${name}\n`)
        }
        results.push({
          sample,
          summary: `Updated ${sample}.`,
          upstreamCommit: 'b'.repeat(40),
          outputDigest: `digest-${sample}`,
        } as SyncResult)
      }

      prepareHandoffBundle({
        planFile,
        resultsDirectory,
        results,
        output,
      })

      assert.deepEqual(readdirSync(output).sort(), ['plan.json', 'samples'])
      assert.deepEqual(readdirSync(path.join(output, 'samples')).sort(), samples)
      assert.equal(readFileSync(path.join(output, 'plan.json'), 'utf8'), '{"version":3}\n')
      assert.equal(existsSync(path.join(output, 'pr-body.md')), false)
      assert.equal(existsSync(path.join(output, 'handoff.json')), false)
      assert.equal(existsSync(path.join(output, 'pr-title.txt')), false)
      assert.equal(existsSync(path.join(output, 'agent-instructions.md')), false)
      assert.equal(
        readFileSync(path.join(output, 'samples', samples[1]!, 'self-audit.md'), 'utf8'),
        'bot-meetings: self-audit.md\n'
      )
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  })
})

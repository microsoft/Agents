import assert from 'node:assert/strict'
import {
  chmodSync, existsSync, readFileSync, writeFileSync
} from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'
import { migrateCandidate } from '../../src/candidate.js'
import { SyncError } from '../../src/config.js'
import { createPlan } from '../../src/plan.js'
import { statePath } from '../../src/state.js'
import { commandErrors } from '../../src/validation-process.js'
import {
  candidateFixture, commit, selected, versions, write
} from '../fixtures/js-candidate.js'
import type { SyncResult } from '../../src/types.js'

describe('candidate-failures', () => {
  it('candidate failures do not create verified state or a publishable patch', async () => {
    for (const failure of ['outside', 'reference', 'context', 'upstream', 'network', 'generated', 'tracked-generated'] as const) {
      const fixture = candidateFixture()
      const {
        temp, root, upstream, output
      } = fixture
      const originalToken = process.env.GITHUB_TOKEN
      process.env.GITHUB_TOKEN = 'offline-fixture-token'
      let implementationTurns = 0
      try {
        if (failure === 'tracked-generated') {
          write(fixture.sampleRoot, 'coverage/unvalidated.js', '// existing generated file')
          commit(root)
        }
        write(root, '.sync/plan.json', createPlan(root, upstream, versions, selected.sample, 'js'))
        const implement = () => {
          implementationTurns++
          fixture.importSample()
          if (failure === 'outside') {
            write(root, 'outside.txt', 'outside selected destination')
          }
          if (failure === 'reference') {
            write(root, 'canonical/index.ts', 'tampered reference')
          }
          if (failure === 'context') {
            const context = path.join(root, '.sync/context/sync-context.json')
            chmodSync(context, 0o644)
            writeFileSync(context, '{}')
          }
          if (failure === 'upstream') {
            write(upstream, 'samples/TeamsSDK/bot-cards/nodejs/bot-cards/index.ts', 'tampered source')
          }
          if (failure === 'generated' || failure === 'tracked-generated') {
            write(fixture.sampleRoot, 'coverage/unvalidated.js', '// excluded, unvalidated change')
          }
        }
        const runtime = failure === 'network'
          ? {
              ...fixture.runtime,
              runCommand: async () => {
                throw new SyncError('Validation infrastructure failed: ENOTFOUND registry.npmjs.org')
              },
            }
          : fixture.runtime
        const exit = await migrateCandidate(root, {
          language: 'js',
          sample: selected.sample,
          'upstream-root': upstream,
          plan: path.join(root, '.sync/plan.json'),
          'output-directory': output,
        }, {
          sdkFactory: fixture.sdkFactory(implement, true),
          validationRuntime: runtime
        })
        assert.equal(exit, 1, failure)
        const result = JSON.parse(readFileSync(path.join(output, 'sync-result.json'), 'utf8')) as SyncResult
        assert.equal(result.publishable, false, failure)
        assert.equal(existsSync(statePath(root, selected)), false, failure)
        assert.equal(existsSync(path.join(output, 'change.patch')), false, failure)
        const expected = {
          outside: /outside selected sample/,
          reference: /outside selected sample|frozen plan/,
          context: /protected context/,
          upstream: /Upstream worktree changed/,
          network: /infrastructure failed/,
          generated: /Generated output cannot be included/,
          'tracked-generated': /Generated output cannot be included/,
        }[failure]!
        assert.match(result.error!, expected)
        assert.equal(implementationTurns, 1, 'Infrastructure/guard failures must not trigger source repair')
      } finally {
        if (originalToken === undefined) {
          delete process.env.GITHUB_TOKEN
        } else {
          process.env.GITHUB_TOKEN = originalToken
        }
        removeFixtureDirectory(temp)
      }
    }
  })

  it('npm network errors are infrastructure failures rather than source diagnostics', async () => {
    await assert.rejects(
      commandErrors(process.execPath, ['-e', 'console.error("npm error ENOTFOUND registry.npmjs.org"); process.exit(1)'], process.cwd()),
      /Validation infrastructure failed/
    )
  })
})

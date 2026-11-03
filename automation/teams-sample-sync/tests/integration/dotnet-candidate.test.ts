import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'
import { migrateCandidate } from '../../src/candidate.js'
import { targetId } from '../../src/identity.js'
import { createPlan } from '../../src/plan.js'
import { verifyPatch } from '../../src/verify-patch.js'
import { statePath } from '../../src/state.js'
import {
  candidateFixture, commit, git, selected, versions, write
} from '../fixtures/dotnet-candidate.js'
import type { SyncResult } from '../../src/types.js'

describe('dotnet-candidate', () => {
  it('offline .NET import produces a verified patch and qualified state, then a state-only update', async () => {
    const fixture = candidateFixture()
    const {
      temp, root, upstream, sampleRoot, output
    } = fixture
    const originalToken = process.env.GITHUB_TOKEN
    process.env.GITHUB_TOKEN = 'offline-fixture-token'
    try {
      assert.equal(existsSync(sampleRoot), false)
      const plan = createPlan(root, upstream, versions, selected.sample, 'dotnet')
      const planFile = path.join(root, '.sync/plan.json')
      write(root, '.sync/plan.json', plan)
      const options = {
        language: 'dotnet',
        sample: selected.sample,
        'upstream-root': upstream,
        plan: planFile,
        'output-directory': output,
      }
      assert.equal(await migrateCandidate(root, options, {
        sdkFactory: fixture.sdkFactory(fixture.importSample, true),
        validationRuntime: fixture.runtime,
      }), 0)
      const resultFile = path.join(output, 'sync-result.json')
      const result = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(result.status, 'updated')
      assert.equal(result.validation?.language, 'dotnet')
      assert.ok(existsSync(statePath(root, selected)))
      assert.ok(fixture.commands.some(([command, action]) => command === 'dotnet' && action === 'test'))
      const verification = {
        language: 'dotnet',
        sample: selected.sample,
        result: resultFile
      }
      assert.doesNotThrow(() => verifyPatch(root, verification))
      const applied = path.join(temp, 'applied')
      git(temp, '-c', 'core.autocrlf=false', 'clone', '--quiet', '--no-hardlinks', root, applied)
      git(applied, 'config', 'core.autocrlf', 'false')
      git(applied, 'apply', '--index', '--binary', path.join(output, 'change.patch'))
      assert.doesNotThrow(() => verifyPatch(applied, verification))
      commit(root)
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'dotnet').matrix, [])

      write(upstream, 'samples/TeamsSDK/bot-cards/dotnet/bot-cards/Program.cs', '// updated source guidance')
      commit(upstream)
      const incremental = createPlan(root, upstream, versions, selected.sample, 'dotnet')
      assert.equal(incremental.samples[targetId(selected)]!.status, 'pending')
      write(root, '.sync/plan.json', incremental)
      assert.equal(await migrateCandidate(root, options, {
        sdkFactory: fixture.sdkFactory(() => {}, false),
        validationRuntime: fixture.runtime,
      }), 0)
      const unchanged = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(unchanged.status, 'no-changes')
      assert.deepEqual(unchanged.destinationChanges, ['automation/teams-sample-sync/state/dotnet/bot-cards.lock.json'])
      assert.doesNotThrow(() => verifyPatch(root, verification))
    } finally {
      if (originalToken === undefined) {
        delete process.env.GITHUB_TOKEN
      } else {
        process.env.GITHUB_TOKEN = originalToken
      }
      removeFixtureDirectory(temp)
    }
  })
})

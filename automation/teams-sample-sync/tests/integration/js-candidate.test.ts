import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'
import { migrateCandidate } from '../../src/candidate.js'
import { artifactName, targetId } from '../../src/identity.js'
import { createPlan } from '../../src/plan.js'
import { verifyPatch } from '../../src/verify-patch.js'
import { publishableResults } from '../../src/publish-bundle.js'
import { statePath } from '../../src/state.js'
import {
  candidateFixture, commit, git, selected, versions, write
} from '../fixtures/js-candidate.js'
import type { SyncResult } from '../../src/types.js'

describe('js-candidate', () => {
  it('offline JS initial import produces a verifiable patch and incremental state, then a state-only update', async () => {
    const fixture = candidateFixture()
    const {
      temp, root, upstream, sampleRoot, output
    } = fixture
    const originalToken = process.env.GITHUB_TOKEN
    process.env.GITHUB_TOKEN = 'offline-fixture-token'
    try {
      assert.equal(existsSync(sampleRoot), false)
      const plan = createPlan(root, upstream, versions, selected.sample, 'js')
      const planFile = path.join(root, '.sync/plan.json')
      write(root, '.sync/plan.json', plan)
      const options = {
        language: 'js',
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
      assert.equal(result.validation?.language, 'js')
      assert.ok(existsSync(path.join(sampleRoot, 'package-lock.json')))
      assert.ok(existsSync(path.join(sampleRoot, 'manifest/color.png')))
      assert.ok(existsSync(statePath(root, selected)))
      assert.ok(fixture.commands.some((args) => args.includes('--ignore-scripts')))
      const verification = {
        language: 'js',
        sample: selected.sample,
        result: resultFile
      }
      assert.doesNotThrow(() => verifyPatch(root, verification))
      const applied = path.join(temp, 'applied')
      git(temp, '-c', 'core.autocrlf=false', 'clone', '--quiet', '--no-hardlinks', root, applied)
      git(applied, 'config', 'core.autocrlf', 'false')
      git(applied, 'apply', '--index', '--binary', path.join(output, 'change.patch'))
      assert.doesNotThrow(() => verifyPatch(applied, verification))
      write(applied, 'samples/nodejs/teams/bot-cards/coverage/unvalidated.js', '// excluded payload')
      assert.throws(() => verifyPatch(applied, verification), /Generated output cannot be included/)
      const originalState = readFileSync(statePath(root, selected), 'utf8')
      write(root, path.relative(root, statePath(root, selected)), {
        ...result.state,
        outputDigest: 'tampered'
      })
      assert.throws(() => verifyPatch(root, verification), /patch does not apply|Applied state differs/)
      write(root, path.relative(root, statePath(root, selected)), originalState)

      const artifact = path.join(root, '.sync/artifacts', artifactName(selected))
      write(artifact, 'sync-result.json', result)
      assert.equal(publishableResults(plan, path.dirname(artifact), result.baseSha).length, 1)
      write(artifact, 'sync-result.json', {
        ...result,
        language: 'dotnet'
      })
      assert.throws(() => publishableResults(plan, path.dirname(artifact), result.baseSha), /not publishable/)
      write(root, '.sync/results/sync-result.json', {
        ...result,
        language: 'dotnet'
      })
      assert.throws(() => verifyPatch(root, verification), /complete validated migration/)
      write(root, '.sync/results/sync-result.json', result)
      const source = readFileSync(path.join(sampleRoot, 'src/agent.ts'), 'utf8')
      write(sampleRoot, 'src/agent.ts', `${source}\n// tampered output`)
      assert.throws(() => verifyPatch(root, verification), /patch does not apply|output digest/)
      write(sampleRoot, 'src/agent.ts', source)
      assert.doesNotThrow(() => verifyPatch(root, verification))
      commit(root)
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'js').matrix, [])

      write(upstream, 'samples/TeamsSDK/bot-cards/nodejs/bot-cards/index.ts', '// changed upstream documentation')
      commit(upstream)
      const incremental = createPlan(root, upstream, versions, selected.sample, 'js')
      assert.equal(incremental.samples[targetId(selected)]!.status, 'pending')
      write(root, '.sync/plan.json', incremental)
      assert.equal(await migrateCandidate(root, options, {
        sdkFactory: fixture.sdkFactory(() => {}, false),
        validationRuntime: fixture.runtime,
      }), 0)
      const unchanged = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(unchanged.status, 'no-changes')
      assert.equal(unchanged.previousUpstreamCommit, plan.upstreamCommit)
      assert.deepEqual(unchanged.destinationChanges, ['automation/teams-sample-sync/state/js/bot-cards.lock.json'])
      assert.doesNotThrow(() => verifyPatch(root, verification))
      assert.equal(git(root, 'diff', '--name-only', '--', 'samples/nodejs/teams'), '')
    } finally {
      if (originalToken === undefined) {
        delete process.env.GITHUB_TOKEN
      } else {
        process.env.GITHUB_TOKEN = originalToken
      }
      removeFixtureDirectory(temp)
    }
  })

  it('an existing destination without state is reconciled without discarding local behavior', async () => {
    const fixture = candidateFixture()
    const {
      temp, root, upstream, sampleRoot, output
    } = fixture
    const originalToken = process.env.GITHUB_TOKEN
    process.env.GITHUB_TOKEN = 'offline-fixture-token'
    try {
      fixture.importSample()
      for (const name of ['color.png', 'outline.png']) {
        write(sampleRoot, `manifest/${name}`, 'icon')
      }
      await fixture.runtime.runCommand('npm', ['install'], sampleRoot)
      write(sampleRoot, 'local-notes.md', 'Preserve local destination guidance.')
      commit(root)
      const plan = createPlan(root, upstream, versions, selected.sample, 'js')
      assert.equal(plan.samples[targetId(selected)]!.status, 'pending')
      write(root, '.sync/plan.json', plan)
      const options = {
        language: 'js',
        sample: selected.sample,
        'upstream-root': upstream,
        plan: path.join(root, '.sync/plan.json'),
        'output-directory': output,
      }
      assert.equal(await migrateCandidate(root, options, {
        sdkFactory: fixture.sdkFactory(() => {}, false),
        validationRuntime: fixture.runtime,
      }), 0)
      assert.equal(readFileSync(path.join(sampleRoot, 'local-notes.md'), 'utf8'), 'Preserve local destination guidance.')
      const resultFile = path.join(output, 'sync-result.json')
      const result = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(result.status, 'no-changes')
      assert.deepEqual(result.destinationChanges, ['automation/teams-sample-sync/state/js/bot-cards.lock.json'])
      assert.doesNotThrow(() => verifyPatch(root, {
        language: 'js',
        sample: selected.sample,
        result: resultFile
      }))
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

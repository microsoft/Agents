import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'
import { migrateCandidate } from '../../src/candidate.js'
import { targetId } from '../../src/identity.js'
import { createPlan } from '../../src/plan.js'
import { publishableResults } from '../../src/publish-bundle.js'
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

  it('.NET validation repairs once and blocks failed candidates without advancing verified state', async () => {
    for (const failure of ['repairable-build', 'persistent-build', 'sample-tests', 'shared-harness', 'package-feed'] as const) {
      const fixture = candidateFixture()
      const {
        temp, root, upstream, output
      } = fixture
      const originalToken = process.env.GITHUB_TOKEN
      process.env.GITHUB_TOKEN = 'offline-fixture-token'
      let implementationTurns = 0
      let buildRuns = 0
      let contractRuns = 0
      let sampleTestRuns = 0
      let smokeRuns = 0
      try {
        const plan = createPlan(root, upstream, versions, selected.sample, 'dotnet')
        write(root, '.sync/plan.json', plan)
        const harness = path.join(root, 'contracts/TeamsSampleSync.ContractTests.csproj')
        const originalHarness = readFileSync(harness, 'utf8')
        const exit = await migrateCandidate(root, {
          language: 'dotnet',
          sample: selected.sample,
          'upstream-root': upstream,
          plan: path.join(root, '.sync/plan.json'),
          'output-directory': output,
        }, {
          sdkFactory: fixture.sdkFactory(() => {
            implementationTurns++
            fixture.importSample()
            if (failure === 'sample-tests') {
              write(fixture.sampleRoot, 'tests/Sample.Tests.csproj', '<Project />')
            }
          }, true),
          validationRuntime: {
            ...fixture.runtime,
            runCommand: async (command, args, cwd) => {
              if (args[0] === 'restore' && failure === 'package-feed') {
                throw new Error('Validation infrastructure failed: NU1301 package feed unavailable')
              }
              if (args[0] === 'build') {
                buildRuns++
                if (failure === 'persistent-build' || (failure === 'repairable-build' && buildRuns === 1)) {
                  return ['error CS0117: Selected SDK API no longer exists']
                }
              }
              if (args[0] === 'test' && args[1] === harness) {
                contractRuns++
                if (failure === 'shared-harness') {
                  return ['error CS0117: Protected contract harness uses an incompatible SDK API']
                }
              } else if (args[0] === 'test') {
                sampleTestRuns++

                return ['Sample behavior assertion failed']
              }

              return fixture.runtime.runCommand(command, args, cwd)
            },
            runHttpSmoke: async () => {
              smokeRuns++

              return []
            },
          },
        })
        const result = JSON.parse(readFileSync(path.join(output, 'sync-result.json'), 'utf8')) as SyncResult
        assert.equal(readFileSync(harness, 'utf8'), originalHarness, failure)
        assert.equal(implementationTurns, failure === 'package-feed' ? 1 : 2, failure)
        if (failure === 'repairable-build') {
          assert.equal(exit, 0)
          assert.equal(buildRuns, 2)
          assert.equal(contractRuns, 1)
          assert.equal(smokeRuns, 1)
          assert.equal(result.publishable, true)
          assert.equal(result.validation?.group, 'all')
          assert.equal(result.validation?.checks.contracts?.status, 'passed')
          assert.ok(existsSync(statePath(root, selected)))
          assert.doesNotThrow(() => verifyPatch(root, {
            language: 'dotnet',
            sample: selected.sample,
            result: path.join(output, 'sync-result.json')
          }))
        } else {
          assert.equal(exit, 1, failure)
          assert.equal(result.status, 'failed', failure)
          assert.equal(result.publishable, false, failure)
          assert.equal(existsSync(statePath(root, selected)), false, failure)
          assert.equal(existsSync(path.join(output, 'change.patch')), false, failure)
          write(root, '.sync/publication/teams-sample-sync-dotnet--bot-cards/sync-result.json', result)
          assert.throws(() => publishableResults(plan, path.join(root, '.sync/publication'), result.baseSha), /not publishable/)
          if (failure === 'persistent-build') {
            assert.equal(buildRuns, 2)
            assert.equal(contractRuns, 0)
            assert.equal(smokeRuns, 0)
            assert.match(result.error!, /CS0117/)
          } else if (failure === 'sample-tests') {
            assert.equal(sampleTestRuns, 2)
            assert.match(result.error!, /Sample behavior assertion failed/)
          } else if (failure === 'shared-harness') {
            assert.equal(contractRuns, 2)
            assert.match(result.error!, /Protected contract harness/)
          } else {
            assert.equal(buildRuns, 0)
            assert.match(result.error!, /NU1301/)
          }
        }
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
})

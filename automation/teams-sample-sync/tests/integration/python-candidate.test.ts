// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { existsSync, readFileSync, rmSync } from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { migrateCandidate } from '../../src/candidate.js'
import { createPlan } from '../../src/plan.js'
import { verifyPatch } from '../../src/verify-patch.js'
import { statePath } from '../../src/state.js'
import { targetId } from '../../src/identity.js'
import { PYTHON_SDK_PACKAGES, selectPythonSdkVersions } from '../../src/python-versions.js'
import { pythonPolicyRuntime } from '../../src/python-runtime.js'
import { SyncError } from '../../src/errors.js'
import { hash, stable } from '../../src/git.js'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'
import {
  candidateFixture, commit, git, selected, write
} from '../fixtures/python-candidate.js'
import type { SyncContext, SyncResult } from '../../src/types.js'

describe('python-candidate', () => {
  it('offline Python import produces a verified patch and isolated state, then an unchanged plan', async () => {
    const fixture = candidateFixture()
    const {
      root, upstream, temp, output, versions
    } = fixture
    const token = process.env.GITHUB_TOKEN
    process.env.GITHUB_TOKEN = 'offline-fixture-token'

    try {
      const plan = createPlan(root, upstream, versions, selected.sample, 'python')
      write(root, '.sync/plan.json', plan)
      const exit = await migrateCandidate(root, {
        language: 'python',
        sample: selected.sample,
        'upstream-root': upstream,
        plan: path.join(root, '.sync/plan.json'),
        'output-directory': output,
      }, {
        sdkFactory: fixture.sdkFactory(fixture.importSample, true),
        validationRuntime: fixture.runtime,
      })
      assert.equal(exit, 0)
      const resultFile = path.join(output, 'sync-result.json')
      const result = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(result.validation?.language, 'python')
      assert.equal(result.status, 'updated')
      const verification = {
        language: 'python',
        sample: selected.sample,
        result: resultFile
      }
      verifyPatch(root, verification)
      const contextFile = path.join(output, 'source-context.json')
      const originalContext = readFileSync(contextFile, 'utf8')
      const context = JSON.parse(originalContext) as SyncContext
      context.migration.packages!['microsoft-agents-activity'] = '1.9.1'
      write(output, 'source-context.json', context)
      write(output, 'sync-result.json', {
        ...result,
        sourceContextDigest: hash(stable(context))
      })
      assert.throws(() => verifyPatch(root, verification), /policy|selection/i)
      write(output, 'source-context.json', originalContext)
      write(output, 'sync-result.json', {
        ...result,
        language: 'js'
      })
      assert.throws(() => verifyPatch(root, verification), /complete validated migration/)
      write(output, 'sync-result.json', result)
      const applied = path.join(temp, 'applied')
      git(temp, '-c', 'core.autocrlf=false', 'clone', '--quiet', '--no-hardlinks', root, applied)
      git(applied, 'config', 'core.autocrlf', 'false')
      git(applied, 'apply', '--index', '--binary', path.join(output, 'change.patch'))
      verifyPatch(applied, verification)
      commit(root)
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'python').matrix, [])

      write(root, 'samples/python/quickstart/src/agent.py', '# Unrelated quickstart change')
      write(root, 'samples/python/proactive/appManifest/color.png', 'unrelated icon')
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'python').matrix, [])
      rmSync(path.join(root, 'samples/python/quickstart/src/agent.py'))
      rmSync(path.join(root, 'samples/python/proactive/appManifest/color.png'))
      const canonical = readFileSync(path.join(root, 'canonical/agent.py'), 'utf8')
      write(root, 'canonical/agent.py', '# Updated maintained Python reference')
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'python').samples[targetId(selected)]!.changedComponents, ['canonicalSample'])
      write(root, 'canonical/agent.py', canonical)
      const icon = readFileSync(path.join(root, 'assets/color.png'), 'utf8')
      write(root, 'assets/color.png', 'updated maintained icon')
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'python').samples[targetId(selected)]!.changedComponents, ['manifestAssets'])
      write(root, 'assets/color.png', icon)
      assert.deepEqual(createPlan(root, upstream, versions, selected.sample, 'python').matrix, [])

      write(upstream, 'samples/TeamsSDK/bot-cards/python/bot-cards/main.py', '# Changed upstream behavior guidance')
      commit(upstream)
      const incremental = createPlan(root, upstream, versions, selected.sample, 'python')
      assert.equal(incremental.samples[targetId(selected)]!.status, 'pending')
      write(root, '.sync/plan.json', incremental)
      assert.equal(await migrateCandidate(root, {
        language: 'python',
        sample: selected.sample,
        'upstream-root': upstream,
        plan: path.join(root, '.sync/plan.json'),
        'output-directory': output,
      }, {
        sdkFactory: fixture.sdkFactory(() => {}, false),
        validationRuntime: fixture.runtime,
      }), 0)
      const unchanged = JSON.parse(readFileSync(resultFile, 'utf8')) as SyncResult
      assert.equal(unchanged.status, 'no-changes')
      assert.deepEqual(unchanged.destinationChanges, ['automation/teams-sample-sync/state/python/bot-cards.lock.json'])
      verifyPatch(root, verification)
      commit(root)

      const newerSdk = selectPythonSdkVersions(pythonPolicyRuntime(), '1.9.0', Object.fromEntries(
        PYTHON_SDK_PACKAGES.map((name) => [name, [{
          name,
          version: '1.9.1',
          requires_python: '>=3.11',
          requires_dist: [],
          yanked: false,
        }]])
      ))
      const sdkChange = createPlan(root, upstream, { python: newerSdk }, selected.sample, 'python')
      assert.ok(sdkChange.samples[targetId(selected)]!.changedComponents.includes('packagePolicy'))

      write(root, 'migration-skill/SKILL.md', 'Updated Python migration guidance')
      const skillChange = createPlan(root, upstream, versions, selected.sample, 'python')
      assert.ok(skillChange.samples[targetId(selected)]!.changedComponents.includes('migrationSkill'))
      git(upstream, 'rm', 'samples/TeamsSDK/bot-cards/python/bot-cards/main.py')
      commit(upstream)
      assert.equal(createPlan(root, upstream, versions, selected.sample, 'python').samples[targetId(selected)]!.status, 'upstream-removed')
    } finally {
      if (token === undefined) {
        delete process.env.GITHUB_TOKEN
      } else {
        process.env.GITHUB_TOKEN = token
      }

      removeFixtureDirectory(temp)
    }
  })

  it('Python validation repairs once and persistent failures never advance state', async () => {
    for (const failure of ['repair-once', 'persistent-build', 'contracts', 'restore', 'network', 'environment'] as const) {
      const fixture = candidateFixture()
      const {
        root, upstream, output, versions, temp
      } = fixture
      const token = process.env.GITHUB_TOKEN
      process.env.GITHUB_TOKEN = 'offline-fixture-token'
      let builds = 0

      try {
        write(root, '.sync/plan.json', createPlan(root, upstream, versions, selected.sample, 'python'))
        const exit = await migrateCandidate(root, {
          language: 'python',
          sample: selected.sample,
          'upstream-root': upstream,
          plan: path.join(root, '.sync/plan.json'),
          'output-directory': output,
        }, {
          sdkFactory: fixture.sdkFactory(fixture.importSample, true),
          validationRuntime: {
            ...fixture.runtime,
            runCommand: async (command, args, cwd) => {
              if (args[1]?.endsWith('python_prepare.py')) {
                if (failure === 'restore') {
                  return ['No matching distribution satisfies candidate requirements']
                }

                if (failure === 'network') {
                  throw new SyncError('Validation infrastructure failed: getaddrinfo failed')
                }
              }

              if (args[3] === 'compile') {
                builds++

                if (failure === 'persistent-build' || (failure === 'repair-once' && builds === 1)) {
                  return ['Python syntax compilation failed']
                }
              }

              if (failure === 'contracts' && args[3] === 'contracts') {
                return ['Protected Python behavior failed']
              }

              if (failure === 'environment' && args[3] === 'contracts') {
                write(path.dirname(path.dirname(command)), 'changed-package.py', '# Candidate modified the environment')
              }

              return fixture.runtime.runCommand(command, args, cwd)
            },
          },
        })
        assert.equal(exit, failure === 'repair-once' ? 0 : 1)
        assert.equal(existsSync(statePath(root, selected)), failure === 'repair-once')
        assert.equal(builds >= 2, !['restore', 'network', 'environment'].includes(failure))
        const result = JSON.parse(readFileSync(path.join(output, 'sync-result.json'), 'utf8')) as SyncResult
        assert.equal(result.publishable, failure === 'repair-once')
      } finally {
        if (token === undefined) {
          delete process.env.GITHUB_TOKEN
        } else {
          process.env.GITHUB_TOKEN = token
        }

        removeFixtureDirectory(temp)
      }
    }
  })

  it('Python outcome reconciliation corrects one mismatch and blocks a continuing mismatch', async () => {
    for (const reconcile of [true, false]) {
      const fixture = candidateFixture()
      const {
        root, upstream, output, versions, temp
      } = fixture
      const token = process.env.GITHUB_TOKEN
      process.env.GITHUB_TOKEN = 'offline-fixture-token'
      let implementationTurns = 0

      try {
        write(root, '.sync/plan.json', createPlan(root, upstream, versions, selected.sample, 'python'))
        const exit = await migrateCandidate(root, {
          language: 'python',
          sample: selected.sample,
          'upstream-root': upstream,
          plan: path.join(root, '.sync/plan.json'),
          'output-directory': output,
        }, {
          sdkFactory: fixture.sdkFactory(() => {
            implementationTurns++
            fixture.importSample()
          }, () => reconcile && implementationTurns > 1),
          validationRuntime: fixture.runtime,
        })
        assert.equal(implementationTurns, 2)
        assert.equal(exit, reconcile ? 0 : 1)
        assert.equal(existsSync(statePath(root, selected)), reconcile)
        const result = JSON.parse(readFileSync(path.join(output, 'sync-result.json'), 'utf8')) as SyncResult
        assert.equal(result.publishable, reconcile)
      } finally {
        if (token === undefined) {
          delete process.env.GITHUB_TOKEN
        } else {
          process.env.GITHUB_TOKEN = token
        }

        removeFixtureDirectory(temp)
      }
    }
  })
})

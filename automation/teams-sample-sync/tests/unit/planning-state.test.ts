import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { parse, stringify } from 'yaml'
import { digestDirectory } from '../../src/git.js'
import { createPlan } from '../../src/plan.js'
import { createState, statePath } from '../../src/state.js'
import type { Configuration, PlanSample, ValidationResult } from '../../src/types.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const id = 'dotnet--bot-cards'
const versions = {
  dotnet: {
    minimumAgentsSdkVersion: '1.8',
    selectedAgentsSdkVersion: '1.8.*',
    releaseTag: 'v1.8.77'
  },
}

function write (root: string, relative: string, content: string): void {
  const file = path.join(root, relative)
  mkdirSync(path.dirname(file), { recursive: true })
  writeFileSync(file, content)
}

function commit (root: string): void {
  execFileSync('git', ['add', '.'], { cwd: root })
  execFileSync('git', [
    '-c', 'user.name=Sync Tests', '-c', 'user.email=tests@example.com', 'commit', '-qm', 'Fixture snapshot',
  ], { cwd: root })
}

function fixture (): {
  root: string
  upstream: string
} {
  const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-plan-'))
  const configured = parse(readFileSync(path.join(repo, 'automation/teams-sample-sync/config/targets.yml'), 'utf8')) as Configuration
  const profile = configured.profiles.dotnet!
  configured.profiles = { dotnet: profile }
  profile.samples = { 'bot-cards': profile.samples['bot-cards']! }
  profile.canonicalSample = 'canonical'
  profile.manifestAssets = 'assets'
  profile.migrationSkill = 'migration-skill'
  profile.syncSkill = 'sync-skill'
  profile.contractDirectory = 'contracts'
  configured.manifestSkill = 'manifest-skill'
  write(root, 'automation/teams-sample-sync/config/targets.yml', stringify(configured))
  write(root, 'automation/teams-sample-sync/config/ownership.yml', 'version: 1\nprotectedPaths: []\noutputDigestExcludes: []\n')
  write(root, 'automation/teams-sample-sync/prompts/agent-prompt.md', 'Migrate the selected sample.')
  write(root, 'automation/teams-sample-sync/package.json', '{}')
  write(root, 'automation/teams-sample-sync/package-lock.json', '{}')
  write(root, 'samples/dotnet/teams/bot-cards/Program.cs', 'destination')
  write(root, 'migration-skill/SKILL.md', 'Migration instructions')
  write(root, 'canonical/Program.cs', 'reference')
  write(root, 'assets/color.png', 'icon')
  const upstream = path.join(root, 'upstream')
  write(upstream, 'samples/TeamsSDK/bot-cards/dotnet/bot-cards/Program.cs', 'source')
  write(upstream, 'samples/TeamsSDK/node-only/nodejs/index.ts', 'node source')
  execFileSync('git', ['init', '-q'], { cwd: upstream })
  execFileSync('git', ['config', 'core.autocrlf', 'false'], { cwd: upstream })
  commit(upstream)

  return {
    root,
    upstream
  }
}

function saveVerifiedState (root: string, entry: PlanSample): void {
  const identity = {
    language: entry.language,
    sample: entry.sample
  }
  const destination = 'dotnet'
  const validation: ValidationResult = {
    version: 3,
    ...identity,
    id: 'validation',
    passed: true,
    repairable: true,
    group: 'all',
    outputDigest: digestDirectory(path.join(root, `samples/${destination}/teams/${entry.sample}`)),
    checks: {},
    errors: [],
    externalValidationRequired: [],
  }
  const file = statePath(root, identity)
  mkdirSync(path.dirname(file), { recursive: true })
  writeFileSync(file, JSON.stringify(createState(identity, entry, validation)))
}

describe('incremental planning with namespaced state', () => {
  it('recognizes unchanged targets, output drift, and migration input changes', () => {
    const { root, upstream } = fixture()
    try {
      const initial = createPlan(root, upstream, versions)
      assert.deepEqual(initial.matrix.map((item) => item.id), [id])
      assert.equal(initial.samples[id]!.status, 'pending')
      saveVerifiedState(root, initial.samples[id]!)
      assert.deepEqual(createPlan(root, upstream, versions).matrix, [])

      write(root, 'samples/dotnet/teams/bot-cards/Program.cs', 'manual drift')
      assert.equal(createPlan(root, upstream, versions).samples[id]!.status, 'pending')
      write(root, 'samples/dotnet/teams/bot-cards/Program.cs', 'destination')
      write(root, 'migration-skill/SKILL.md', 'New migration requirements')
      const changed = createPlan(root, upstream, versions).samples[id]!
      assert.equal(changed.status, 'pending')
      assert.deepEqual(changed.changedComponents, ['migrationSkill'])
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('invalidates plans on reference and icon edits, ignores former sample inputs, and rejects missing references', () => {
    const { root, upstream } = fixture()
    try {
      const initial = createPlan(root, upstream, versions)
      saveVerifiedState(root, initial.samples[id]!)
      write(root, 'samples/dotnet/quickstart/Program.cs', 'unrelated quickstart')
      write(root, 'samples/nodejs/proactive/appManifest/color.png', 'unrelated icon')
      assert.deepEqual(createPlan(root, upstream, versions).matrix, [])
      write(root, 'canonical/Program.cs', 'updated reference')
      const changed = createPlan(root, upstream, versions)
      assert.deepEqual(changed.samples[id]!.changedComponents, ['canonicalSample'])
      saveVerifiedState(root, changed.samples[id]!)
      write(root, 'assets/color.png', 'updated icon')
      assert.deepEqual(createPlan(root, upstream, versions).samples[id]!.changedComponents, ['manifestAssets'])
      rmSync(path.join(root, 'canonical'), { recursive: true })
      assert.throws(() => createPlan(root, upstream, versions), /Missing migration input canonicalSample/)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('filters candidate discovery by language and reports source removal', () => {
    const { root, upstream } = fixture()
    try {
      write(upstream, 'samples/TeamsSDK/new-dotnet/dotnet/Program.cs', 'new source')
      commit(upstream)
      const initial = createPlan(root, upstream, versions)
      assert.deepEqual(initial.newSampleCandidates, [{
        language: 'dotnet',
        sample: 'new-dotnet',
        status: 'new-sample-candidate',
      }])
      rmSync(path.join(upstream, 'samples/TeamsSDK/bot-cards'), { recursive: true })
      commit(upstream)
      const removed = createPlan(root, upstream, versions)
      assert.equal(removed.samples[id]!.status, 'upstream-removed')
      assert.deepEqual(removed.matrix, [])
      assert.throws(() => createPlan(root, upstream, versions, 'missing-sample'), /Sample is not selected/)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})

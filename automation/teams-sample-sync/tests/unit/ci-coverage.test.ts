import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { minimatch } from 'minimatch'
import { parse } from 'yaml'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

function workflow (name: string) {
  return parse(readFileSync(path.join(repo, '.github/workflows', name), 'utf8'))
}

describe('ci-coverage', () => {
  it('repository lint runs even when a PR changes no sample files', () => {
    const samples = workflow('cd-samples.yml')
    const lint = samples.jobs.lint
    assert.equal(lint.if, undefined)
    assert.equal(lint.needs, undefined)
    assert.equal(samples.on.pull_request.paths, undefined)
    assert.ok(lint.steps.some((step: { run?: string }) => step.run === 'npm run lint'))
    assert.equal(samples.permissions.contents, 'read')
    const buildSteps = samples.jobs['build-node'].steps as Array<{
      name?: string
      run?: string
    }>
    const install = buildSteps.findIndex((step) => step.run?.startsWith('npm ci'))
    const build = buildSteps.findIndex((step) => step.name === 'Build all Node samples')
    assert.ok(install >= 0 && install < build, 'Legacy samples need the root TypeScript build dependency')
  })

  it('automation CI covers engine changes and frozen guidance, assets and contracts', () => {
    const automation = workflow('ci-teams-sample-sync.yml')
    const patterns = automation.on.pull_request.paths as string[]
    const relevantChanges = [
      'automation/teams-sample-sync/src/state.ts',
      'automation/teams-sample-sync/config/targets.yml',
      'automation/teams-sample-sync/tests/contracts/TeamsSampleContracts.cs',
      'agent-plugins/agents-for-net/skills/migration/SKILL.md',
      '.claude-plugin/marketplace.json',
      'samples/dotnet/teams/bot-cards/BotCardsAgent.cs',
      'samples/nodejs/teams/bot-cards/src/agent.ts',
      'automation/teams-sample-sync/reference/dotnet/appManifest/color.png',
      'automation/teams-sample-sync/reference/nodejs/src/index.ts',
      'automation/teams-sample-sync/reference/manifest-assets/nodejs/color.png',
      '.github/workflows/ci-teams-sample-sync.yml',
      '.github/workflows/sync-teams-dotnet-samples.yml',
      '.github/workflows/cd-samples.yml',
      'eslint.config.js',
      'package.json',
      'package-lock.json',
    ]
    for (const changed of relevantChanges) {
      assert.ok(patterns.some((pattern) => minimatch(changed, pattern, { dot: true })), changed)
    }
    for (const unrelated of ['docs/unrelated.md', 'samples/dotnet/quickstart/Program.cs', 'samples/nodejs/quickstart/src/index.ts', 'samples/nodejs/proactive/appManifest/color.png']) {
      assert.equal(patterns.some((pattern) => minimatch(unrelated, pattern)), false, unrelated)
    }
    assert.deepEqual(automation.on.pull_request.branches, ['main'])
    assert.equal(automation.permissions.contents, 'read')
    const job = automation.jobs.automation
    assert.equal(job.if, undefined)
    assert.equal(job.env.TEAMS_SYNC_SDK_SMOKE, '0')
    assert.equal(job.defaults.run['working-directory'], 'automation/teams-sample-sync')
    assert.ok(job.steps.some((step: { run?: string }) => step.run === 'npm run build'))
    assert.ok(job.steps.some((step: { run?: string }) => step.run === 'npm test'))
  })
})

import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, rmSync, writeFileSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { publishableResults } from '../../src/publish-bundle.js'
import { artifactName } from '../../src/identity.js'
import type { Plan, SyncResult } from '../../src/types.js'

const baseSha = 'a'.repeat(40)
const upstreamCommit = 'b'.repeat(40)
const samples = ['bot-cards', 'bot-meetings']
const plan: Plan = {
  version: 4,
  upstreamCommit,
  agentsSdkVersions: {
    dotnet: {
      minimumAgentsSdkVersion: '1',
      selectedAgentsSdkVersion: '1',
      releaseTag: 'v1'
    }
  },
  samples: Object.fromEntries(samples.map((sample) => [`dotnet--${sample}`, {
    language: 'dotnet',
    sample,
    status: 'pending',
    sourceTree: 'tree',
    inputDigest: 'input',
    componentDigests: {},
    changedComponents: [],
  }])),
  matrix: samples.map((sample) => ({
    language: 'dotnet',
    sample,
    id: `dotnet--${sample}`,
    upstreamCommit
  })),
  newSampleCandidates: [],
}

function result (sample: string, overrides: Partial<SyncResult> = {}): SyncResult {
  return {
    version: 5,
    language: 'dotnet',
    sample,
    status: 'updated',
    publishable: true,
    baseSha,
    previousUpstreamCommit: null,
    upstreamCommit,
    upstreamChanges: [],
    changedComponents: [],
    copilot: {
      sdkVersion: '1',
      runtimeVersion: '1'
    },
    observedModels: [],
    sourceTree: 'tree',
    sourceContextDigest: 'context',
    inputDigest: 'input',
    componentDigests: {},
    diagnostics: [],
    summary: 'Updated the card action and its manifest.',
    validation: {
      version: 3,
      language: 'dotnet',
      id: 'validation',
      sample,
      passed: true,
      repairable: false,
      outputDigest: 'digest',
      group: 'all',
      checks: Object.fromEntries(['project', 'restore', 'build', 'manifest', 'httpSmoke', 'contracts'].map((name) => [name, {
        status: 'passed',
        errors: []
      }])),
      errors: [],
      externalValidationRequired: [],
    },
    ...overrides,
  }
}

function writeResult (directory: string, value: SyncResult): void {
  const sampleDirectory = path.join(directory, artifactName(value))
  mkdirSync(sampleDirectory, { recursive: true })
  writeFileSync(path.join(sampleDirectory, 'sync-result.json'), JSON.stringify(value))
}

describe('coordinated publication gate', () => {
  it('requires both same-name language variants before handing off a mixed run', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-mixed-results-'))
    try {
      const mixed = structuredClone(plan)
      const sample = samples[0]!
      mixed.matrix = [plan.matrix[0]!, {
        language: 'js',
        sample,
        id: `js--${sample}`,
        upstreamCommit
      }]
      mixed.samples[`js--${sample}`] = {
        ...plan.samples[`dotnet--${sample}`]!,
        language: 'js'
      }
      writeResult(directory, result(sample))
      assert.throws(() => publishableResults(mixed, directory, baseSha), /Missing migration result/)
      const js = result(sample, { language: 'js' })
      js.validation!.language = 'js'
      js.validation!.checks.packages = {
        status: 'passed',
        errors: []
      }
      writeResult(directory, js)
      assert.deepEqual(publishableResults(mixed, directory, baseSha).map((item) => item.language), ['dotnet', 'js'])
      writeResult(directory, {
        ...js,
        publishable: false,
        status: 'failed'
      })
      assert.throws(() => publishableResults(mixed, directory, baseSha), /not publishable/)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('accepts all planned results in plan order', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[1]!))
      writeResult(directory, result(samples[0]!))
      assert.deepEqual(publishableResults(plan, directory, baseSha).map((item) => item.sample), samples)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('blocks the entire publication when one result is missing or failed', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      assert.throws(() => publishableResults(plan, directory, baseSha), /Missing migration result for bot-meetings/)
      writeResult(directory, result(samples[1]!, {
        status: 'failed',
        publishable: false
      }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects a result from a different base commit', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { baseSha: 'c'.repeat(40) }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('does not publish a changed sample without a summary', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { summary: '' }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('accepts a validated sample with a longer summary', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { summary: 'Verified meeting behavior. '.repeat(12) }))
      assert.equal(publishableResults(plan, directory, baseSha).length, 2)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects cross-language artifacts and evidence that differs from the plan', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-evidence-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!))
      writeFileSync(
        path.join(directory, artifactName({
          language: 'dotnet',
          sample: samples[1]!
        }), 'sync-result.json'),
        JSON.stringify(result(samples[1]!, { language: 'js' }))
      )
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable/)
      writeResult(directory, result(samples[1]!, { inputDigest: 'different-input' }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable/)
      const missingContracts = result(samples[1]!)
      missingContracts.validation!.checks.contracts = {
        status: 'skipped',
        errors: []
      }
      writeResult(directory, missingContracts)
      assert.throws(() => publishableResults(plan, directory, baseSha), /Required validation check did not pass/)
    } finally {
      rmSync(directory, {
        recursive: true,
        force: true
      })
    }
  })
})

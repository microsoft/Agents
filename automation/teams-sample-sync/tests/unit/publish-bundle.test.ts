import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { publishableResults } from '../../src/publish-bundle.js'
import type { Plan, SyncResult } from '../../src/types.js'

const baseSha = 'a'.repeat(40)
const upstreamCommit = 'b'.repeat(40)
const samples = ['bot-cards', 'bot-meetings']
const plan: Plan = {
  version: 3,
  upstreamCommit,
  agentsSdkVersion: { minimumAgentsSdkVersion: '1', selectedAgentsSdkVersion: '1', releaseTag: 'v1' },
  samples: {},
  matrix: samples.map((sample) => ({ sample, upstreamCommit })),
  newSampleCandidates: [],
}

function result (sample: string, overrides: Partial<SyncResult> = {}): SyncResult {
  return {
    version: 4,
    sample,
    status: 'updated',
    publishable: true,
    baseSha,
    previousUpstreamCommit: null,
    upstreamCommit,
    upstreamChanges: [],
    changedComponents: [],
    copilot: { sdkVersion: '1', runtimeVersion: '1' },
    observedModels: [],
    sourceTree: 'tree',
    sourceContextDigest: 'context',
    inputDigest: 'input',
    componentDigests: {},
    diagnostics: [],
    summary: 'Updated the card action and its manifest.',
    validation: {
      version: 2,
      id: 'validation',
      sample,
      passed: true,
      repairable: false,
      outputDigest: 'digest',
      group: 'all',
      checks: {},
      errors: [],
      externalValidationRequired: [],
    },
    ...overrides,
  }
}

function writeResult (directory: string, value: SyncResult): void {
  const sampleDirectory = path.join(directory, `teams-sample-sync-${value.sample}`)
  mkdirSync(sampleDirectory, { recursive: true })
  writeFileSync(path.join(sampleDirectory, 'sync-result.json'), JSON.stringify(value))
}

describe('coordinated publication gate', () => {
  it('accepts all planned results in plan order', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[1]!))
      writeResult(directory, result(samples[0]!))
      assert.deepEqual(publishableResults(plan, directory, baseSha).map((item) => item.sample), samples)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('blocks the entire publication when one result is missing or failed', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      assert.throws(() => publishableResults(plan, directory, baseSha), /Missing migration result for bot-meetings/)
      writeResult(directory, result(samples[1]!, { status: 'failed', publishable: false }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('rejects a result from a different base commit', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { baseSha: 'c'.repeat(40) }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('does not publish a changed sample without a summary', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { summary: '' }))
      assert.throws(() => publishableResults(plan, directory, baseSha), /not publishable for bot-meetings/)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('accepts a validated sample with a longer summary', () => {
    const directory = mkdtempSync(path.join(tmpdir(), 'teams-sync-results-'))
    try {
      writeResult(directory, result(samples[0]!))
      writeResult(directory, result(samples[1]!, { summary: 'Verified meeting behavior. '.repeat(12) }))
      assert.equal(publishableResults(plan, directory, baseSha).length, 2)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
})

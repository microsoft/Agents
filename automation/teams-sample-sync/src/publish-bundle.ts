/** Checks that a coordinated run has a complete set of publishable sample results. */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import type { Plan, SyncResult } from './types.js'

export function publishableResults (plan: Plan, directory: string, baseSha: string): SyncResult[] {
  if (plan.version !== 3 || plan.matrix.length === 0) {
    throw new SyncError('No planned samples are available to publish')
  }
  const seen = new Set<string>()
  return plan.matrix.map(({ sample, upstreamCommit }) => {
    if (seen.has(sample)) {
      throw new SyncError(`Duplicate planned sample: ${sample}`)
    }
    seen.add(sample)
    const file = path.join(directory, `teams-sample-sync-${sample}`, 'sync-result.json')
    if (!existsSync(file)) {
      throw new SyncError(`Missing migration result for ${sample}`)
    }
    let result: SyncResult
    try {
      result = JSON.parse(readFileSync(file, 'utf8')) as SyncResult
    } catch (error) {
      throw new SyncError(`Invalid migration result for ${sample}: ${String(error)}`)
    }
    if (
      result.version !== 4 ||
      result.sample !== sample ||
      result.baseSha !== baseSha ||
      result.upstreamCommit !== upstreamCommit ||
      !result.publishable ||
      !['updated', 'no-changes'].includes(result.status) ||
      !result.summary?.trim() ||
      !result.validation?.passed
    ) {
      throw new SyncError(`Migration result is not publishable for ${sample}`)
    }
    return result
  })
}

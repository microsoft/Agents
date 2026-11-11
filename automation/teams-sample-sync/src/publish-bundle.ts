/** Checks that a coordinated run has a complete set of publishable sample results. */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import { artifactName, sameTarget, targetId } from './identity.js'
import { stable } from './git.js'
import { assertFullValidation } from './validate.js'
import type { Plan, SyncResult } from './types.js'

export function publishableResults (plan: Plan, directory: string, baseSha: string): SyncResult[] {
  if (plan.version !== 4 || plan.matrix.length === 0) {
    throw new SyncError('No planned samples are available to publish')
  }

  const seen = new Set<string>()

  return plan.matrix.map((selected) => {
    const { sample, upstreamCommit, id } = selected
    const entry = plan.samples[id]

    if (targetId(selected) !== id || !entry || !sameTarget(entry, selected) || entry.status !== 'pending') {
      throw new SyncError(`Invalid planned target: ${id}`)
    }

    if (seen.has(id)) {
      throw new SyncError(`Duplicate planned sample: ${sample}`)
    }

    seen.add(id)
    const file = path.join(directory, artifactName(selected), 'sync-result.json')

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
      result.version !== 5 ||
      !sameTarget(result, selected) ||
      result.baseSha !== baseSha ||
      result.upstreamCommit !== upstreamCommit ||
      upstreamCommit !== plan.upstreamCommit ||
      result.sourceTree !== entry.sourceTree ||
      result.inputDigest !== entry.inputDigest ||
      stable(result.componentDigests) !== stable(entry.componentDigests) ||
      !result.publishable ||
      !['updated', 'no-changes'].includes(result.status) ||
      !result.summary?.trim() ||
      !result.validation?.passed
    ) {
      throw new SyncError(`Migration result is not publishable for ${sample}`)
    }

    assertFullValidation(result.validation!, sample, undefined, selected.language)

    return result
  })
}

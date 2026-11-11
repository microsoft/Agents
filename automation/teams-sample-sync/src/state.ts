/**
 * Defines the persisted verified-state contract for each destination sample.
 * A state file records the accepted upstream tree, all input digests, and the validated output digest; malformed
 * or incomplete state is rejected instead of being trusted for an incremental no-change decision.
 */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import { identity, sameTarget, type TargetIdentity } from './identity.js'
import type { PlanSample, State, ValidationResult } from './types.js'
export function statePath (repo: string, selected: TargetIdentity): string {
  identity(selected)

  return path.join(repo, 'automation/teams-sample-sync/state', selected.language, `${selected.sample}.lock.json`)
}

const REQUIRED_COMPONENT_DIGESTS = [
  'sourceTree',
  'target',
  'protection',
  'migrationSkill',
  'manifestSkill',
  'canonicalSample',
  'manifestAssets',
  'copilot',
  'packagePolicy',
  'validator',
  'syncSkill',
]

function validateStateEnvelope (value: State, selected: TargetIdentity): void {
  if (
    value.version !== 3 ||
    !sameTarget(value, selected) ||
    value.status !== 'verified' ||
    typeof value.upstreamCommit !== 'string' ||
    typeof value.sourceTree !== 'string' ||
    typeof value.inputDigest !== 'string' ||
    typeof value.outputDigest !== 'string' ||
    !value.componentDigests ||
    typeof value.componentDigests !== 'object'
  ) {
    throw new SyncError('Invalid version-3 state')
  }
}

export function readState (repo: string, selected: TargetIdentity): State | undefined {
  const file = statePath(repo, selected)

  if (!existsSync(file)) {
    return undefined
  }

  let value: unknown

  try {
    value = JSON.parse(readFileSync(file, 'utf8'))
  } catch {
    throw new SyncError(`Invalid state JSON: ${file}`)
  }

  if (!value || typeof value !== 'object' || (value as { version?: unknown }).version !== 3) {
    throw new SyncError(`Invalid namespaced state: ${file}`)
  }

  const candidate = value as State
  validateStateEnvelope(candidate, selected)
  validateState(candidate, selected)

  return candidate
}

export function readPriorState (repo: string, selected: TargetIdentity): Pick<State, 'upstreamCommit' | 'sourceTree'> | undefined {
  const current = readState(repo, selected)

  if (current) {
    return {
      upstreamCommit: current.upstreamCommit,
      sourceTree: current.sourceTree
    }
  }

  // Legacy state is history only, and only belongs to .NET. It is not trusted
  // as validated output under the new profile and validator inputs.
  if (selected.language !== 'dotnet') {
    return undefined
  }

  identity(selected)
  const file = path.join(repo, 'automation/teams-sample-sync/state', `${selected.sample}.lock.json`)

  if (!existsSync(file)) {
    return undefined
  }

  let value: unknown

  try {
    value = JSON.parse(readFileSync(file, 'utf8'))
  } catch {
    throw new SyncError(`Invalid state JSON: ${file}`)
  }

  if (!value || typeof value !== 'object') {
    return undefined
  }

  const candidate = value as Record<string, unknown>

  if (
    (candidate.version !== 1 && candidate.version !== 2) ||
    candidate.sample !== selected.sample ||
    typeof candidate.upstreamCommit !== 'string' ||
    typeof candidate.sourceTree !== 'string'
  ) {
    return undefined
  }

  return {
    upstreamCommit: candidate.upstreamCommit,
    sourceTree: candidate.sourceTree
  }
}

export function validateState (value: State, selected: TargetIdentity): void {
  validateStateEnvelope(value, selected)

  if (REQUIRED_COMPONENT_DIGESTS.some((key) => typeof value.componentDigests[key] !== 'string')) {
    throw new SyncError('Version-3 state has incomplete component digests')
  }
}

export function createState (selected: TargetIdentity, plan: PlanSample, validation: ValidationResult): State {
  if (!sameTarget(plan, selected) || !sameTarget(validation, selected) || !validation.passed || !plan.upstreamCommit || !plan.sourceTree || !plan.inputDigest || !plan.componentDigests) {
    throw new SyncError('Cannot create state before successful validation')
  }

  return {
    version: 3,
    ...identity(selected),
    upstreamCommit: plan.upstreamCommit,
    sourceTree: plan.sourceTree,
    inputDigest: plan.inputDigest,
    outputDigest: validation.outputDigest,
    componentDigests: plan.componentDigests,
    status: 'verified',
  }
}

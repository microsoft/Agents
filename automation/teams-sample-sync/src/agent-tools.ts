/** For Copilot Agents only: trusted migration-tool infrastructure. */
/**
 * Defines the trusted custom tools available to the Copilot session.
 * Each tool rechecks the pinned upstream tree, frozen context, and selected-sample boundary before it
 * returns data or validation results, so untrusted agent output cannot widen the migration scope.
 */
import { assertAgentChanges, assertContext, assertUpstream } from './guard.js'
import { SyncError } from './config.js'
import { digestDirectory } from './git.js'
import path from 'node:path'
import { validateSample, type ValidationRuntime } from './validate.js'
import type { SyncContext, Targets, ValidationResult } from './types.js'

export interface ToolHost {
  repo: string
  upstream: string
  baseSha: string
  sampleRoot: string
  sourcePath: string
  upstreamCommit: string
  sourceTree: string
  protectedPaths: string[]
  excludes: string[]
  contextRoot: string
  contextDigest: string
  context: SyncContext
  configured: Targets
  validationRuntime?: ValidationRuntime
  onValidation?: (validation: ValidationResult) => void
}

let activeValidation = false
export function guardCandidate (host: ToolHost): void {
  assertAgentChanges(host.repo, host.baseSha, host.sampleRoot, host.protectedPaths)
  assertContext(host.contextRoot, host.contextDigest)
  assertUpstream(host.upstream, host.upstreamCommit, host.sourcePath, host.sourceTree)
}
export async function validateTool (host: ToolHost, group: 'code' | 'manifest' | 'all'): Promise<ValidationResult> {
  if (activeValidation) {
    throw new SyncError('Validation is already running')
  }
  guardCandidate(host)
  activeValidation = true
  try {
    const validation = await validateSample(
      host.repo,
      host.context.sample,
      host.sampleRoot,
      host.configured,
      host.context.manifest,
      host.excludes,
      host.context.migration.selectedAgentsSdkVersion,
      host.validationRuntime,
      group
    )
    guardCandidate(host)
    if (validation.outputDigest !== digestDirectory(path.join(host.repo, host.sampleRoot), host.excludes)) {
      throw new SyncError('Validation returned a stale candidate digest')
    }
    host.onValidation?.(validation)
    return validation
  } finally {
    activeValidation = false
  }
}

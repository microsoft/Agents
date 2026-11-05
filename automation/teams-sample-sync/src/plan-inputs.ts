/** Recomputable migration inputs bind each phase to the same policy and source. */
import { existsSync, readFileSync, statSync } from 'node:fs'
import path from 'node:path'
import { targetProtection, SyncError } from './config.js'
import { digestDirectory, hash, stable } from './git.js'
import type { AgentsSdkVersionSelection, Targets } from './types.js'

export function migrationInputs (
  repo: string,
  configured: Targets,
  agentsSdkVersion: AgentsSdkVersionSelection,
  sourceTree: string,
  sample: string
): Record<string, string> {
  const target = configured.samples[sample]

  if (!target) {
    throw new SyncError(`Sample is not configured: ${sample}`)
  }

  for (const [name, relative] of Object.entries({
    canonicalSample: configured.canonicalSample,
    manifestAssets: configured.manifestAssets
  })) {
    const input = path.join(repo, relative)

    if (!existsSync(input) || !statSync(input).isDirectory()) {
      throw new SyncError(`Missing migration input ${name}: ${relative}; restore the maintained reference before planning`)
    }
  }

  const owner = targetProtection(repo, configured)

  return {
    sourceTree,
    target: hash(
      stable({
        language: configured.language,
        configurationVersion: configured.version,
        upstream: configured.upstream,
        destinationRoot: configured.destinationRoot,
        canonicalSample: configured.canonicalSample,
        sample: target,
      })
    ),
    protection: hash(stable(owner)),
    migrationSkill: digestDirectory(path.join(repo, configured.migrationSkill)),
    manifestSkill: digestDirectory(path.join(repo, configured.manifestSkill)),
    syncSkill: digestDirectory(
      path.join(repo, configured.syncSkill)
    ),
    canonicalSample: digestDirectory(path.join(repo, configured.canonicalSample), owner.outputDigestExcludes),
    manifestAssets: digestDirectory(path.join(repo, configured.manifestAssets)),
    copilot: hash(stable(configured.copilot)),
    packagePolicy: hash(
      stable({
        ...configured.packagePolicy,
        selection: agentsSdkVersion,
      })
    ),
    validator: hash(
      stable([
        configured.validatorVersion,
        configured.copilot.sdkVersion,
        configured.copilot.runtimeVersion,
        readFileSync(path.join(repo, 'automation/teams-sample-sync/prompts/agent-prompt.md'), 'utf8'),
        digestDirectory(path.join(repo, 'automation/teams-sample-sync/src')),
        digestDirectory(path.join(repo, 'automation/teams-sample-sync/scripts')),
        digestDirectory(path.join(repo, configured.contractDirectory), owner.outputDigestExcludes),
        readFileSync(path.join(repo, 'automation/teams-sample-sync/package.json'), 'utf8'),
        readFileSync(path.join(repo, 'automation/teams-sample-sync/package-lock.json'), 'utf8'),
      ])
    ),
  }
}

export function assertMigrationInputs (
  repo: string,
  configured: Targets,
  selection: AgentsSdkVersionSelection,
  sourceTree: string,
  expected: Record<string, string>,
  sample: string
): void {
  if (stable(migrationInputs(repo, configured, selection, sourceTree, sample)) !== stable(expected)) {
    throw new SyncError('Migration inputs differ from the frozen plan')
  }
}

/** For Copilot Agents only: migration planning infrastructure. */
/**
 * Creates the immutable plan consumed by the matrix workflow.
 * It compares upstream trees, migration inputs, and the current destination digest with verified state; only
 * changed or manually drifted samples enter the migration matrix, while new/upstream-removed samples are reported.
 */
import path from 'node:path'
import { protection, SyncError, targets } from './config.js'
import { digestDirectory, git, hash, stable, tree } from './git.js'
import { readFileSync } from 'node:fs'
import { readState } from './state.js'
import type { Plan, PlanSample } from './types.js'

export function createPlan (repo: string, upstream: string, chosen?: string): Plan {
  const configured = targets(repo)
  const owner = protection(repo)
  const commit = git(upstream, ['rev-parse', 'HEAD']) as string
  const names = chosen ? [chosen] : Object.keys(configured.samples).sort()
  if (chosen && !configured.samples[chosen]) throw new SyncError(`Sample is not selected: ${chosen}`)

  const samples: Plan['samples'] = {}
  const matrix: Plan['matrix'] = []
  for (const name of names) {
    const target = configured.samples[name]!
    const sourcePath = `${configured.upstream.root}/${target.source}`
    const sourceTree = tree(upstream, commit, sourcePath)
    const previousState = readState(repo, name)
    if (!sourceTree) {
      samples[name] = { status: 'upstream-removed', changedComponents: [], previousState }
      continue
    }
    const componentDigests = {
      sourceTree,
      target: hash(stable({
        upstream: configured.upstream,
        destinationRoot: configured.destinationRoot,
        canonicalSample: configured.canonicalSample,
        sample: target,
      })),
      protection: hash(stable(owner)),
      migrationSkill: digestDirectory(path.join(repo, configured.migrationSkill)),
      manifestSkill: digestDirectory(path.join(repo, configured.manifestSkill)),
      syncSkill: digestDirectory(path.join(repo, 'automation/teams-sample-sync/skills/sync-teams-dotnet-samples-to-agents-sdk')),
      canonicalSample: digestDirectory(path.join(repo, configured.canonicalSample), owner.outputDigestExcludes),
      copilot: hash(stable(configured.copilot)),
      packagePolicy: hash(stable(configured.packagePolicy)),
      validator: hash(stable([configured.validatorVersion, configured.copilot.sdkVersion, configured.copilot.runtimeVersion,
        readFileSync(path.join(repo, 'automation/teams-sample-sync/prompts/agent-prompt.md'), 'utf8'),
        digestDirectory(path.join(repo, 'automation/teams-sample-sync/src')),
        digestDirectory(path.join(repo, 'automation/teams-sample-sync/tests/contracts'), owner.outputDigestExcludes),
        readFileSync(path.join(repo, 'automation/teams-sample-sync/package.json'), 'utf8'),
        readFileSync(path.join(repo, 'automation/teams-sample-sync/package-lock.json'), 'utf8')])),
    }
    const inputDigest = hash(stable(componentDigests))
    const destinationDigest = digestDirectory(
      path.join(repo, configured.destinationRoot, target.destination),
      owner.outputDigestExcludes
    )
    const changedComponents = Object.entries(componentDigests)
      .filter(([key, value]) => previousState?.componentDigests[key] !== value)
      .map(([key]) => key)
      .sort()
    const status = previousState?.inputDigest === inputDigest && previousState.outputDigest === destinationDigest
      ? 'unchanged'
      : 'pending'
    const entry: PlanSample = {
      status,
      upstreamCommit: commit,
      sourceTree,
      inputDigest,
      componentDigests,
      changedComponents,
      previousState,
    }
    samples[name] = entry
    if (status === 'pending') matrix.push({ sample: name, upstreamCommit: commit })
  }

  const inventory = (git(upstream, ['ls-tree', '-d', '--name-only', `${commit}:${configured.upstream.root}`]) as string)
    .split(/\r?\n/).filter((name) => name !== '' && name.toLowerCase() !== 'archived')
  return {
    version: 2,
    upstreamCommit: commit,
    samples,
    matrix,
    newSampleCandidates: inventory.filter((name) => !(name in configured.samples)).sort()
      .map((sample) => ({ sample, status: 'new-sample-candidate' as const })),
  }
}

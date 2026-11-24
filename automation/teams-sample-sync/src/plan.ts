/**
 * Creates the immutable plan consumed by the matrix workflow.
 * It compares upstream trees, migration inputs, and the current destination digest with verified state; only
 * changed or manually drifted samples enter the migration matrix, while new/upstream-removed samples are reported.
 */
import path from 'node:path'
import {
  targetProtection,
  selectedLanguages,
  SyncError,
  targets
} from './config.js'
import {
  digestDirectory,
  git,
  hash,
  stable,
  tree
} from './git.js'
import { migrationInputs } from './plan-inputs.js'
import { readState } from './state.js'
import { assertSdkSelection } from './sdk-policy.js'
import { targetId, type Language } from './identity.js'
import type { AgentsSdkVersionSelection, Plan, PlanSample } from './types.js'

export function createPlan (
  repo: string,
  upstream: string,
  agentsSdkVersions: Partial<Record<Language, AgentsSdkVersionSelection>>,
  chosen?: string,
  selectedLanguage = 'all'
): Plan {
  const commit = git(upstream, ['rev-parse', 'HEAD']) as string
  const samples: Plan['samples'] = {}
  const matrix: Plan['matrix'] = []
  const newSampleCandidates: Plan['newSampleCandidates'] = []
  let selectedCount = 0

  for (const language of selectedLanguages(repo, selectedLanguage)) {
    const configured = targets(repo, language)
    const agentsSdkVersion = agentsSdkVersions[language]

    if (!agentsSdkVersion) {
      throw new SyncError(`Missing SDK selection for ${language}`)
    }

    assertSdkSelection(configured, agentsSdkVersion)
    const owner = targetProtection(repo, configured)
    const names = Object.keys(configured.samples).filter((name) => !chosen || name === chosen).sort()
    selectedCount += names.length

    for (const name of names) {
      const selected = {
        language,
        sample: name
      }
      const id = targetId(selected)
      const target = configured.samples[name]!
      const sourcePath = `${configured.upstream.root}/${target.source}`
      const sourceTree = tree(upstream, commit, sourcePath)
      const previousState = readState(repo, selected)

      if (!sourceTree) {
        samples[id] = {
          ...selected,
          status: 'upstream-removed',
          changedComponents: [],
          previousState
        }
        continue
      }

      const componentDigests = migrationInputs(repo, configured, agentsSdkVersion, sourceTree, name)
      const inputDigest = hash(stable(componentDigests))
      const destinationDigest = digestDirectory(
        path.join(repo, configured.destinationRoot, target.destination),
        owner.outputDigestExcludes
      )
      const changedComponents = Object.entries(componentDigests)
        .filter(([key, value]) => previousState?.componentDigests[key] !== value)
        .map(([key]) => key)
        .sort()
      const status =
        previousState?.inputDigest === inputDigest && previousState.outputDigest === destinationDigest
          ? 'unchanged'
          : 'pending'
      const entry: PlanSample = {
        ...selected,
        status,
        upstreamCommit: commit,
        sourceTree,
        inputDigest,
        componentDigests,
        changedComponents,
        previousState,
      }
      samples[id] = entry

      if (status === 'pending') {
        matrix.push({
          ...selected,
          id,
          upstreamCommit: commit
        })
      }
    }

    const inventory = (git(upstream, ['ls-tree', '-d', '--name-only', `${commit}:${configured.upstream.root}`]) as string)
      .split(/\r?\n/)
      .filter((name) => name !== '' && name.toLowerCase() !== 'archived')
    const sourceVariant = language === 'js' ? 'nodejs' : 'dotnet'
    const unconfiguredSamples = inventory.filter((name) => !(name in configured.samples)).sort()

    for (const sample of unconfiguredSamples) {
      const variantPath = `${configured.upstream.root}/${sample}/${sourceVariant}`

      if (!tree(upstream, commit, variantPath)) {
        continue
      }

      newSampleCandidates.push({
        language,
        sample,
        status: 'new-sample-candidate' as const
      })
    }
  }

  if (!selectedCount) {
    throw new SyncError(`Sample is not selected: ${chosen ?? 'all'}`)
  }

  return {
    version: 4,
    upstreamCommit: commit,
    agentsSdkVersions,
    samples,
    matrix,
    newSampleCandidates,
  }
}

/** Verifies applied patch paths, evidence, output and namespaced state. */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { targetProtection, targets, SyncError } from './config.js'
import {
  changedPaths,
  digestDirectory,
  git,
  hash,
  matches,
  stable
} from './git.js'
import { language, sameTarget } from './identity.js'
import { readJson, required, resolveOption } from './cli-io.js'
import { statePath, validateState } from './state.js'
import { assertMigrationInputs } from './plan-inputs.js'
import { assertSdkSelection } from './sdk-policy.js'
import { assertFullValidation } from './validate.js'
import { assertOutcomeMatchesSampleChanges } from './sync-session.js'
import { assertPublishableSamplePaths } from './guard.js'
import type { State, SyncContext, SyncResult } from './types.js'

export function verifyPatch (repo: string, values: Record<string, string>): void {
  const sample = required(values, 'sample')
  const selected = {
    language: language(required(values, 'language')),
    sample
  }
  const resultFile = resolveOption(required(values, 'result'))
  const result = readJson<SyncResult>(resultFile)

  if (
    result.version !== 5 ||
    !sameTarget(result, selected) ||
    !['updated', 'no-changes'].includes(result.status) ||
    !result.publishable ||
    !result.state ||
    !result.outputDigest ||
    !result.planHash ||
    !result.validation?.passed
  ) {
    throw new SyncError('Only a complete validated migration is publishable')
  }

  const contextFile = path.join(path.dirname(resultFile), 'source-context.json')
  const planFile = path.join(path.dirname(resultFile), 'migration-plan.md')

  if (
    !existsSync(contextFile) ||
    !existsSync(planFile) ||
    hash(readFileSync(planFile, 'utf8').trim()) !== result.planHash
  ) {
    throw new SyncError('Trusted source context or frozen migration plan is missing or changed')
  }

  const context = readJson<SyncContext>(contextFile)

  if (
    context.version !== 3 ||
    hash(stable(context)) !== result.sourceContextDigest ||
    !sameTarget(context, selected) ||
    context.upstream.currentCommit !== result.upstreamCommit ||
    context.upstream.currentTree !== result.sourceTree
  ) {
    throw new SyncError('Source context does not match the validated result')
  }

  const configured = targets(repo, selected.language)
  const owner = targetProtection(repo, configured)
  const target = configured.samples[sample]

  if (
    !target ||
    stable(result.copilot) !== stable(configured.copilot) ||
    result.componentDigests.copilot !== hash(stable(result.copilot))
  ) {
    throw new SyncError('Copilot configuration differs from validated result')
  }

  assertSdkSelection(configured, context.migration)
  const {
    canonicalSample: _canonicalSample, targetFramework: _targetFramework, nodeVersion: _nodeVersion, ...selection
  } = context.migration
  assertMigrationInputs(repo, configured, selection, result.sourceTree, result.componentDigests, sample)

  if (
    result.validation!.outputDigest !== result.outputDigest ||
    result.inputDigest !== hash(stable(result.componentDigests)) ||
    context.paths.destination !== `${configured.destinationRoot}/${target.destination}` ||
    context.upstream.sourcePath !== `${configured.upstream.root}/${target.source}` ||
    context.upstream.repository !== configured.upstream.repository
  ) {
    throw new SyncError('Validation or source context differs from the configured target')
  }

  assertFullValidation(result.validation!, sample, undefined, selected.language)
  const head = git(repo, ['rev-parse', 'HEAD']) as string

  if (head !== result.baseSha) {
    throw new SyncError('Publish checkout differs from validated base SHA')
  }

  const sampleRelative = `${configured.destinationRoot}/${target.destination}`.replaceAll('\\', '/')
  const stateRelative = path.relative(repo, statePath(repo, selected)).replaceAll('\\', '/')
  const changed = changedPaths(repo, head)
  assertPublishableSamplePaths(changed, sampleRelative, owner.outputDigestExcludes)

  if (!result.destinationChanges || stable(changed) !== stable(result.destinationChanges)) {
    throw new SyncError('Applied paths differ from validated result')
  }

  const sampleChanges = changed.filter((item) => item.startsWith(`${sampleRelative}/`))
  assertOutcomeMatchesSampleChanges(result.status === 'updated' ? 'changed' : 'no-changes', sampleChanges)

  for (const item of changed) {
    if (
      item === 'manifest-evidence.md' ||
      item.endsWith('/manifest-evidence.md') ||
      matches(item, owner.protectedPaths) ||
      (item !== stateRelative && !item.startsWith(`${sampleRelative}/`))
    ) {
      throw new SyncError(`Patch changes prohibited path: ${item}`)
    }
  }

  const patchFile = path.join(path.dirname(resultFile), 'change.patch')

  if (!existsSync(patchFile)) {
    throw new SyncError('Result patch is missing')
  }

  git(repo, ['apply', '--check', '--reverse', '--binary', patchFile])

  if (digestDirectory(path.join(repo, sampleRelative), owner.outputDigestExcludes) !== result.outputDigest) {
    throw new SyncError('Applied sample output digest differs from validated result')
  }

  const state = readJson<State>(path.join(repo, stateRelative))
  validateState(state, selected)

  if (
    stable(state) !== stable(result.state) ||
    state.outputDigest !== result.outputDigest ||
    state.upstreamCommit !== result.upstreamCommit ||
    state.sourceTree !== result.sourceTree ||
    state.inputDigest !== result.inputDigest ||
    hash(stable(state.componentDigests)) !== hash(stable(result.componentDigests))
  ) {
    throw new SyncError('Applied state differs from validated result')
  }
}

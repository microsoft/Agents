/** Produces one isolated, verified migration candidate. */
import type { Tool } from '@github/copilot-sdk'
import { existsSync, mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { CopilotAgentRunner, type ImplementationSession, type SdkFactory } from './agent-runner.js'
import { createContext } from './context.js'
import { targetProtection, targets, SyncError } from './config.js'
import { guardCandidate, validateTool, type ToolHost } from './agent-tools.js'
import {
  changedPaths,
  git,
  hash,
  stable
} from './git.js'
import { failureReport, workflowSummary } from './report.js'
import { createState, statePath } from './state.js'
import { prepareManifest, type ValidationRuntime } from './validate.js'
import { assertMigrationInputs } from './plan-inputs.js'
import { assertSdkSelection } from './sdk-policy.js'
import { assertPublishableSamplePaths } from './guard.js'
import { language, sameTarget, targetId } from './identity.js'
import { assertOutcomeMatchesSampleChanges, runMigrationSession } from './sync-session.js'
import {
  emitFailure,
  readJson,
  required,
  resolveOption,
  writeJson
} from './cli-io.js'
import type { Plan, SyncContext, SyncResult } from './types.js'

function tool (
  name: string,
  description: string,
  parameters: NonNullable<Tool['parameters']>,
  handler: (value: unknown) => Promise<unknown>
): Tool {
  return {
    name,
    description,
    parameters,
    handler,
    skipPermission: true
  }
}

export async function migrateCandidate (
  repo: string,
  values: Record<string, string>,
  dependencies: {
    sdkFactory?: SdkFactory
    validationRuntime?: ValidationRuntime
  } = {}
): Promise<number> {
  const upstream = resolveOption(required(values, 'upstream-root'))
  const plan = readJson<Plan>(resolveOption(required(values, 'plan')))
  const sample = required(values, 'sample')
  const selected = {
    language: language(required(values, 'language')),
    sample
  }
  const output = resolveOption(required(values, 'output-directory'))
  mkdirSync(output, { recursive: true })
  const configured = targets(repo, selected.language)
  const owner = targetProtection(repo, configured)
  const target = configured.samples[sample]
  const entry = plan.samples[targetId(selected)]

  if (
    plan.version !== 4 ||
    !target ||
    !entry?.upstreamCommit ||
    !entry.sourceTree ||
    !entry.inputDigest ||
    !entry.componentDigests ||
    entry.status !== 'pending'
  ) {
    throw new SyncError('Plan does not contain a pending selected sample')
  }

  if (entry.componentDigests.copilot !== hash(stable(configured.copilot))) {
    throw new SyncError('Copilot configuration differs from the selected plan')
  }

  const selection = plan.agentsSdkVersions[selected.language]

  if (!selection || !sameTarget(entry, selected)) {
    throw new SyncError('Plan language or SDK selection differs from the requested target')
  }

  assertSdkSelection(configured, selection)
  assertMigrationInputs(repo, configured, selection, entry.sourceTree, entry.componentDigests, sample)

  if (entry.inputDigest !== hash(stable(entry.componentDigests))) {
    throw new SyncError('Plan input digest does not match its component evidence')
  }

  const baseSha = git(repo, ['rev-parse', 'HEAD']) as string
  const sampleRelative = `${configured.destinationRoot}/${target.destination}`.replaceAll('\\', '/')
  const sampleRoot = path.join(repo, sampleRelative)
  const contextFiles = createContext(repo, upstream, plan, selected)
  const context = readJson<SyncContext>(contextFiles.file)
  const observedModels: SyncResult['observedModels'] = []
  const agentLog = path.join(output, 'agent-log.txt')
  writeFileSync(agentLog, '', 'utf8')
  writeJson(path.join(output, 'source-context.json'), context)
  const result: SyncResult = {
    version: 5,
    ...selected,
    status: 'failed',
    publishable: false,
    baseSha,
    previousUpstreamCommit: context.upstream.previousCommit,
    upstreamCommit: entry.upstreamCommit,
    upstreamChanges: context.upstream.changes,
    changedComponents: entry.changedComponents,
    copilot: configured.copilot,
    observedModels,
    sourceTree: entry.sourceTree,
    sourceContextDigest: hash(stable(context)),
    inputDigest: entry.inputDigest,
    componentDigests: entry.componentDigests,
    diagnostics: [],
  }
  let implementationSession: ImplementationSession | undefined
  const host: ToolHost = {
    repo,
    upstream,
    baseSha,
    sampleRoot: sampleRelative,
    sourcePath: `${configured.upstream.root}/${target.source}`,
    upstreamCommit: entry.upstreamCommit,
    sourceTree: entry.sourceTree,
    protectedPaths: owner.protectedPaths,
    excludes: owner.outputDigestExcludes,
    contextRoot: contextFiles.root,
    contextDigest: contextFiles.digest,
    context,
    configured,
    canValidateCode: () => implementationSession?.canWrite?.() ?? false,
    ...(dependencies.validationRuntime ? { validationRuntime: dependencies.validationRuntime } : {}),
    onValidation: (validation) => {
      result.validation = validation
    },
  }
  const skillDirectories = [
    path.join(repo, configured.syncSkill),
    path.join(repo, configured.migrationSkill),
    path.join(repo, configured.manifestSkill),
  ]
  const runner = new CopilotAgentRunner(
    repo,
    sampleRelative,
    configured.copilot,
    agentLog,
    observedModels,
    skillDirectories,
    dependencies.sdkFactory,
    () => {
      guardCandidate(host)
    }
  )
  const schema: NonNullable<Tool['parameters']> = {
    type: 'object',
    additionalProperties: false,
    required: ['group'],
    properties: {
      group: {
        type: 'string',
        enum: ['code', 'manifest', 'all']
      }
    },
  }

  try {
    if (!process.env.GITHUB_TOKEN) {
      throw new SyncError('Copilot authentication is unavailable: GITHUB_TOKEN is required')
    }

    for (const skill of skillDirectories) {
      if (!existsSync(path.join(skill, 'SKILL.md'))) {
        throw new SyncError(`Configured skill is unavailable: ${skill}`)
      }
    }

    mkdirSync(sampleRoot, { recursive: true })
    prepareManifest(
      sampleRoot, path.join(repo, configured.canonicalSample), target.manifest,
      path.join(repo, configured.manifestAssets)
    )
    const session = await runner.open([
      tool('validate_sample', 'Run trusted validation for the selected sample.', schema, async (input) =>
        validateTool(host, String((input as { group?: unknown }).group) as 'code' | 'manifest' | 'all')
      ),
    ])
    implementationSession = session

    try {
      const migration = await runMigrationSession({
        language: selected.language,
        sample,
        contextFile: path.relative(repo, contextFiles.file).replaceAll('\\', '/'),
        output,
        session,
        validate: () => validateTool(host, 'all'),
        sampleChanges: () => changedPaths(repo, baseSha).filter((item) => item.startsWith(`${sampleRelative}/`)),
        sampleDiff: () => git(repo, ['diff', '--no-ext-diff', '--unified=3', baseSha, '--', sampleRelative]) as string,
      })
      result.validation = migration.validation
      result.outputDigest = migration.validation.outputDigest
      result.planHash = migration.planHash
      result.summary = migration.summary
      writeFileSync(path.join(output, 'self-audit.md'), `${migration.selfAudit}\n`, 'utf8')
      const sampleChanges = changedPaths(repo, baseSha).filter((item) => item.startsWith(`${sampleRelative}/`))
      assertPublishableSamplePaths(sampleChanges, sampleRelative, owner.outputDigestExcludes)
      assertOutcomeMatchesSampleChanges(migration.outcome, sampleChanges)
      const state = createState(selected, entry, migration.validation)
      const lock = statePath(repo, selected)
      writeJson(lock, state)
      git(repo, ['add', '-N', '--', sampleRelative, path.relative(repo, lock).replaceAll('\\', '/')])
      const patch = git(
        repo,
        ['diff', '--binary', baseSha, '--', sampleRelative, path.relative(repo, lock).replaceAll('\\', '/')],
        true
      ) as Buffer

      if (patch.length === 0) {
        throw new SyncError('Validated migration produced no sample or state patch')
      }

      writeFileSync(path.join(output, 'change.patch'), patch)
      result.status = migration.outcome === 'changed' ? 'updated' : 'no-changes'
      result.publishable = true
      result.destinationChanges = changedPaths(repo, baseSha).filter(
        (item) => item === path.relative(repo, lock).replaceAll('\\', '/') || item.startsWith(`${sampleRelative}/`)
      )
      result.state = state
    } finally {
      await session.close()
    }
  } catch (error) {
    result.error = error instanceof Error ? error.message : String(error)
    result.diagnostics.push(result.error)
  } finally {
    await runner.close().catch((error) => {
      result.publishable = false
      result.status = 'failed'
      result.error = `Runtime cleanup failed: ${String(error)}`
      result.diagnostics.push(result.error)
    })
    writeJson(path.join(output, 'sync-result.json'), result)
    writeFileSync(path.join(output, 'workflow-summary.md'), workflowSummary(result), 'utf8')
    const failure = failureReport(result)

    if (failure) {
      emitFailure(failure)
    }
  }

  return result.publishable ? 0 : 1
}

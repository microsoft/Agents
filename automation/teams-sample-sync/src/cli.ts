#!/usr/bin/env node
/** For Copilot Agents only: workflow command-line entry point. */
/**
 * CLI entry point used by the GitHub Actions plan, migrate, and publish jobs.
 * `plan` writes a deterministic matrix, `migrate` produces an isolated validated patch and artifacts,
 * and `verify-patch` rejects anything whose context, digest, or changed paths no longer match that run.
 */
import type { Tool } from '@github/copilot-sdk'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { CopilotAgentRunner, type SdkFactory } from './agent-runner.js'
import { createContext } from './context.js'
import { protection, targets, SyncError } from './config.js'
import { guardCandidate, validateTool, type ToolHost } from './agent-tools.js'
import { changedPaths, digestDirectory, git, hash, matches, stable } from './git.js'
import { createPlan } from './plan.js'
import { failureReport, githubErrorAnnotation, prBody, workflowSummary } from './report.js'
import { createState, statePath, validateState } from './state.js'
import { prepareManifest, type ValidationRuntime } from './validate.js'
import { assertAgentsSdkVersionSelection, resolveAgentsSdkVersion } from './versions.js'
import { assertOutcomeMatchesSampleChanges, runMigrationSession } from './sync-session.js'
import type { Plan, State, SyncContext, SyncResult } from './types.js'

function parseArgs (items: string[]): Record<string, string> {
  const result: Record<string, string> = {}
  for (let index = 0; index < items.length; index += 2) {
    const option = items[index]
    const value = items[index + 1]
    if (!option?.startsWith('--') || !value || value.startsWith('--')) {
      throw new SyncError(`Invalid argument: ${String(option)}`)
    }
    const key = option.slice(2)
    if (!['repo-root', 'upstream-root', 'plan', 'sample', 'output-directory', 'output', 'result'].includes(key)) {
      throw new SyncError(`Unknown option: ${option}`)
    }
    if (key in result) {
      throw new SyncError(`Duplicate option: ${option}`)
    }
    result[key] = value
  }
  return result
}
function required (values: Record<string, string>, name: string): string {
  const value = values[name]
  if (!value) {
    throw new SyncError(`Missing required option: --${name}`)
  }
  return value
}
function readJson<T> (file: string): T {
  try {
    return JSON.parse(readFileSync(file, 'utf8')) as T
  } catch (error) {
    throw new SyncError(`Cannot read JSON ${file}: ${error instanceof Error ? error.message : String(error)}`)
  }
}
function writeJson (file: string, value: unknown): void {
  mkdirSync(path.dirname(file), { recursive: true })
  writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
}
function resolveOption (value: string): string {
  return path.resolve(process.env.INIT_CWD ?? process.cwd(), value)
}
function tool (
  name: string,
  description: string,
  parameters: NonNullable<Tool['parameters']>,
  handler: (value: unknown) => Promise<unknown>
): Tool {
  return { name, description, parameters, handler, skipPermission: true }
}
function emitFailure (report: { stderr: string; annotation: string }): void {
  process.stderr.write(`${report.stderr}\n`)
  if (process.env.GITHUB_ACTIONS === 'true') {
    process.stdout.write(`${report.annotation}\n`)
  }
}

export async function migrateCandidate (
  repo: string,
  values: Record<string, string>,
  dependencies: { sdkFactory?: SdkFactory; validationRuntime?: ValidationRuntime } = {}
): Promise<number> {
  const upstream = resolveOption(required(values, 'upstream-root'))
  const plan = readJson<Plan>(resolveOption(required(values, 'plan')))
  const sample = required(values, 'sample')
  const output = resolveOption(required(values, 'output-directory'))
  mkdirSync(output, { recursive: true })
  const configured = targets(repo)
  const owner = protection(repo)
  const target = configured.samples[sample]
  const entry = plan.samples[sample]
  if (
    plan.version !== 3 ||
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
  assertAgentsSdkVersionSelection(plan.agentsSdkVersion, configured.packagePolicy.minimumAgentsSdkVersion)
  if (
    entry.componentDigests.packagePolicy !==
    hash(
      stable({
        ...configured.packagePolicy,
        selectedAgentsSdkVersion: plan.agentsSdkVersion.selectedAgentsSdkVersion,
      })
    )
  ) {
    throw new SyncError('Agents SDK version selection differs from the selected plan')
  }
  const baseSha = git(repo, ['rev-parse', 'HEAD']) as string
  const sampleRelative = `${configured.destinationRoot}/${target.destination}`.replaceAll('\\', '/')
  const sampleRoot = path.join(repo, sampleRelative)
  const contextFiles = createContext(repo, upstream, plan, sample)
  const context = readJson<SyncContext>(contextFiles.file)
  const observedModels: SyncResult['observedModels'] = []
  const agentLog = path.join(output, 'agent-log.txt')
  writeFileSync(agentLog, '', 'utf8')
  writeJson(path.join(output, 'source-context.json'), context)
  const result: SyncResult = {
    version: 4,
    sample,
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
    ...(dependencies.validationRuntime ? { validationRuntime: dependencies.validationRuntime } : {}),
    onValidation: (validation) => {
      result.validation = validation
    },
  }
  const skillDirectories = [
    path.join(repo, 'automation/teams-sample-sync/skills/sync-teams-dotnet-samples-to-agents-sdk'),
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
    properties: { group: { type: 'string', enum: ['code', 'manifest', 'all'] } },
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
    prepareManifest(sampleRoot, path.join(repo, configured.canonicalSample), target.manifest)
    const session = await runner.open([
      tool('validate_sample', 'Run trusted validation for the selected sample.', schema, async (input) =>
        validateTool(host, String((input as { group?: unknown }).group) as 'code' | 'manifest' | 'all')
      ),
    ])
    try {
      const migration = await runMigrationSession({
        sample,
        contextFile: path.relative(repo, contextFiles.file).replaceAll('\\', '/'),
        output,
        session,
        validate: () => validateTool(host, 'all'),
      })
      result.validation = migration.validation
      result.outputDigest = migration.validation.outputDigest
      result.planHash = migration.planHash
      result.selfAudit = migration.selfAudit
      writeFileSync(path.join(output, 'self-audit.md'), `${migration.selfAudit}\n`, 'utf8')
      const sampleChanges = changedPaths(repo, baseSha).filter((item) => item.startsWith(`${sampleRelative}/`))
      assertOutcomeMatchesSampleChanges(migration.outcome, sampleChanges)
      const state = createState(sample, entry, migration.validation)
      const lock = statePath(repo, sample)
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
    if (result.publishable) {
      writeFileSync(path.join(output, 'pr-body.md'), prBody(result), 'utf8')
    }
    const failure = failureReport(result)
    if (failure) {
      emitFailure(failure)
    }
  }
  return result.publishable ? 0 : 1
}

async function migrate (repo: string, values: Record<string, string>): Promise<number> {
  return migrateCandidate(repo, values)
}

function verifyPatch (repo: string, values: Record<string, string>): void {
  const sample = required(values, 'sample')
  const resultFile = resolveOption(required(values, 'result'))
  const result = readJson<SyncResult>(resultFile)
  if (
    result.version !== 4 ||
    result.sample !== sample ||
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
    context.version !== 2 ||
    hash(stable(context)) !== result.sourceContextDigest ||
    context.sample !== sample ||
    context.upstream.currentCommit !== result.upstreamCommit ||
    context.upstream.currentTree !== result.sourceTree
  ) {
    throw new SyncError('Source context does not match the validated result')
  }
  const configured = targets(repo)
  const owner = protection(repo)
  const target = configured.samples[sample]
  if (
    !target ||
    stable(result.copilot) !== stable(configured.copilot) ||
    result.componentDigests.copilot !== hash(stable(result.copilot))
  ) {
    throw new SyncError('Copilot configuration differs from validated result')
  }
  assertAgentsSdkVersionSelection(context.migration, configured.packagePolicy.minimumAgentsSdkVersion)
  if (
    result.componentDigests.packagePolicy !==
    hash(
      stable({
        ...configured.packagePolicy,
        selectedAgentsSdkVersion: context.migration.selectedAgentsSdkVersion,
      })
    )
  ) {
    throw new SyncError('Agents SDK version selection differs from validated result')
  }
  const head = git(repo, ['rev-parse', 'HEAD']) as string
  if (head !== result.baseSha) {
    throw new SyncError('Publish checkout differs from validated base SHA')
  }
  const sampleRelative = `${configured.destinationRoot}/${target.destination}`.replaceAll('\\', '/')
  const stateRelative = path.relative(repo, statePath(repo, sample)).replaceAll('\\', '/')
  const changed = changedPaths(repo, head)
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
  validateState(state, sample)
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

export async function main (argv = process.argv.slice(2)): Promise<number> {
  try {
    const [command, ...rest] = argv
    if (command !== 'plan' && command !== 'migrate' && command !== 'verify-patch') {
      throw new SyncError('Expected command: plan, migrate, or verify-patch')
    }
    const values = parseArgs(rest)
    const repo = path.resolve(process.env.INIT_CWD ?? process.cwd(), values['repo-root'] ?? '.')
    if (command === 'plan') {
      const configured = targets(repo)
      const agentsSdkVersion = await resolveAgentsSdkVersion(configured.packagePolicy.minimumAgentsSdkVersion)
      const plan = createPlan(repo, resolveOption(required(values, 'upstream-root')), agentsSdkVersion, values.sample)
      writeJson(resolveOption(required(values, 'output')), plan)
      return 0
    }
    if (command === 'migrate') {
      return migrate(repo, values)
    }
    verifyPatch(repo, values)
    return 0
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    emitFailure({ stderr: message, annotation: githubErrorAnnotation('Teams sample synchronization failed', message) })
    return 2
  }
}
const entry = process.argv[1] ? path.resolve(process.argv[1]) : undefined
if (entry && import.meta.url === pathToFileURL(entry).href) {
  process.exitCode = await main()
}

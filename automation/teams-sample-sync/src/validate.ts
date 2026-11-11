/** Coordinates validation and accepts only current, complete evidence. */
import type { Language } from './identity.js'
import { randomUUID } from 'node:crypto'
import path from 'node:path'
import { digestDirectory } from './git.js'
import { SyncError } from './config.js'
import { validationAdapter } from './languages.js'
import type { LanguageValidation } from './language-validation.js'
import { checkManifest, fetchSchema } from './manifest.js'
import {
  assertValidationActive,
  commandErrors,
  currentValidationGeneration,
  httpSmoke,

} from './validation-process.js'
import type {
  AgentsSdkVersionSelection, ManifestTarget, Targets, ValidationResult
} from './types.js'

// Preserve callers while language-specific orchestration is extracted.
export { checkProject, checkRestoredPackages } from './dotnet-project.js'
export { checkManifest, fetchSchema, prepareManifest } from './manifest.js'
export { cancelValidationProcesses, commandErrors, sanitizedChildEnvironment } from './validation-process.js'

export interface ValidationRuntime {
  runCommand: (command: string, args: string[], cwd: string) => string[] | Promise<string[]>
  runHttpSmoke: (sampleRoot: string, project: string, language?: Language) => Promise<string[]>
  loadSchema?: typeof fetchSchema
}

export const defaultValidationRuntime: ValidationRuntime = {
  runCommand: commandErrors,
  runHttpSmoke: httpSmoke,
}

export async function validateSample (
  repo: string,
  sample: string,
  sampleRoot: string,
  configured: Targets,
  target: ManifestTarget,
  excludes: string[],
  selectedAgentsSdkVersion: string,
  runtime: ValidationRuntime = defaultValidationRuntime,
  group: 'code' | 'manifest' | 'all' = 'all',
  adapter: LanguageValidation = validationAdapter(configured.language),
  selection?: AgentsSdkVersionSelection
): Promise<ValidationResult> {
  if (!['code', 'manifest', 'all'].includes(group)) {
    throw new SyncError('Invalid validation group')
  }

  sampleRoot = path.resolve(repo, sampleRoot)
  const generation = currentValidationGeneration()
  const runCommand: ValidationRuntime['runCommand'] = async (command, args, cwd) => {
    assertValidationActive(generation)
    const failures = await runtime.runCommand(command, args, cwd)
    assertValidationActive(generation)

    return failures
  }

  const guardedRuntime: ValidationRuntime = {
    ...runtime,
    runCommand,
    runHttpSmoke: async (root, project, language) => {
      assertValidationActive(generation)
      const failures = await runtime.runHttpSmoke(root, project, language)
      assertValidationActive(generation)

      return failures
    },
  }
  const checks = await adapter.validateCode({
    repo,
    sample,
    sampleRoot,
    configured,
    selectedAgentsSdkVersion,
    ...(selection ? { selection } : {}),
    runtime: guardedRuntime,
    group,
  })

  if (group !== 'code') {
    const failures = await checkManifest(sampleRoot, target, runtime.loadSchema, adapter.capabilities)
    checks.manifest = {
      status: failures.length ? 'failed' : 'passed',
      errors: failures
    }
  } else {
    checks.manifest = {
      status: 'skipped',
      errors: []
    }
  }

  const errors = Object.values(checks).flatMap((check) => check.errors)

  if (group === 'all') {
    for (const key of adapter.requiredChecks(sample)) {
      if (checks[key]?.status !== 'passed') {
        errors.push(`Required validation check did not pass: ${key}`)
      }
    }
  }

  assertValidationActive(generation)

  return {
    version: 3,
    language: configured.language,
    id: randomUUID(),
    sample,
    group,
    passed:
      errors.length === 0 &&
      Object.values(checks).every((check) => check.status !== 'failed' && check.status !== 'not-run'),
    repairable: true,
    outputDigest: digestDirectory(sampleRoot, excludes),
    checks,
    errors,
    externalValidationRequired: ['Credentialed Teams, Entra, Graph, Azure Bot, and portal behavior when applicable'],
  }
}

export function assertFullValidation (
  value: ValidationResult,
  sample: string,
  adapter: LanguageValidation | undefined = undefined,
  language: Language = 'dotnet'
): void {
  if (
    value.version !== 3 ||
    value.language !== language ||
    value.sample !== sample ||
    value.group !== 'all' ||
    !value.id ||
    !value.outputDigest
  ) {
    throw new SyncError('Current full validation is required')
  }

  if (!value.passed || value.errors.length) {
    const details: string[] = []
    const reportedErrors = new Set<string>()

    for (const [name, check] of Object.entries(value.checks)) {
      if (check.status === 'failed' || check.status === 'not-run' || check.errors.length) {
        const errors = check.errors.join('\n')
        details.push(`${name} (${check.status})${errors ? `: ${errors}` : ''}`)

        for (const error of check.errors) {
          reportedErrors.add(error)
        }
      }
    }

    for (const error of value.errors) {
      if (!reportedErrors.has(error)) {
        details.push(error)
      }
    }

    if (!details.length) {
      details.push('Validation did not pass and supplied no failure details.')
    }

    throw new SyncError(`Full validation failed for ${language}/${sample}:\n${details.join('\n')}`)
  }

  for (const key of (adapter ?? validationAdapter(language)).requiredChecks(sample)) {
    if (value.checks[key]?.status !== 'passed') {
      throw new SyncError(`Required validation check did not pass: ${key}`)
    }
  }

  if (Object.values(value.checks).some((check) => !['passed', 'skipped'].includes(check.status) || check.errors.length)) {
    throw new SyncError('Validation contains failing or incomplete checks')
  }
}

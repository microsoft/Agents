// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Python validation composes trusted tooling behind the shared evidence interface. */
import { readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './errors.js'
import { preparePythonEnvironment, PythonDependencyError, type PythonEnvironment } from './python-environment.js'
import { checkPythonProject } from './python-project.js'
import { pythonCapabilities } from './python-capabilities.js'
import type { PythonPolicyRuntime } from './python-versions.js'
import type { CodeValidationInput, LanguageValidation } from './language-validation.js'
import type { ValidationCheck } from './types.js'

const REQUIRED_CHECKS = ['project', 'restore', 'packages', 'build', 'import', 'manifest', 'contracts', 'httpSmoke']

export function createPythonValidation (tooling: PythonPolicyRuntime): LanguageValidation {
  const validateCode = async (input: CodeValidationInput): Promise<Record<string, ValidationCheck>> => {
    const checks: Record<string, ValidationCheck> = {}
    const record = (name: string, errors: string[]): boolean => {
      checks[name] = {
        status: errors.length ? 'failed' : 'passed',
        errors,
      }

      return errors.length === 0
    }

    const skipRemaining = (reason: string): void => {
      for (const name of REQUIRED_CHECKS.filter((name) => name !== 'manifest' && !checks[name])) {
        checks[name] = {
          status: 'not-run',
          errors: [reason],
        }
      }
    }

    if (input.group === 'manifest') {
      for (const name of REQUIRED_CHECKS.filter((name) => name !== 'manifest')) {
        checks[name] = {
          status: 'skipped',
          errors: []
        }
      }

      return checks
    }

    if (!input.selection) {
      throw new SyncError('Python validation requires the frozen SDK package selection')
    }

    const errors = checkPythonProject(tooling, input.sampleRoot, input.selection)

    if (!record('project', errors)) {
      skipRemaining('Requires a valid Python project')

      return checks
    }

    let environment: PythonEnvironment

    try {
      environment = await preparePythonEnvironment(tooling, input.runtime,
        readFileSync(path.join(input.sampleRoot, 'requirements.txt'), 'utf8'), input.selection.packages!)
    } catch (error) {
      if (!(error instanceof PythonDependencyError)) {
        throw error
      }

      record('restore', error.errors)
      skipRemaining('Requires successful Python dependency resolution')

      return checks
    }

    try {
      writeFileSync(path.join(input.sampleRoot, 'requirements.lock'), environment.lock)
      record('restore', [])
      record('packages', [])
      const script = path.join(input.repo, 'automation/teams-sample-sync/scripts/python_checks.py')
      const run = (mode: string, extra: string[] = []): Promise<string[]> => Promise.resolve(
        input.runtime.runCommand(environment.interpreter, ['-I', '-B', script, mode, input.sampleRoot, ...extra], input.repo)
      )

      for (const [check, mode] of [['build', 'compile'], ['import', 'import']] as const) {
        if (!record(check, await run(mode))) {
          skipRemaining(`Requires successful Python ${check}`)

          return checks
        }
      }

      record('contracts', await run('contracts', [path.join(input.repo, input.configured.contractDirectory, `${input.sample}.py`)]))

      if (input.group === 'all') {
        record('httpSmoke', await run('smoke'))
      } else {
        checks.httpSmoke = {
          status: 'skipped',
          errors: []
        }
      }

      await environment.assertUnchanged()

      return checks
    } finally {
      environment.dispose()
    }
  }

  return {
    validateCode,
    capabilities: (root) => pythonCapabilities(tooling, root),
    requiredChecks: () => [...REQUIRED_CHECKS],
  }
}

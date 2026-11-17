// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Allocates validation-owned state and delegates bounded wheel installation. */
import {
  mkdtempSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { record, SyncError } from './config.js'
import type { PythonToolRuntime } from './python-tool.js'
import type { ValidationRuntime } from './validate.js'

export interface PythonEnvironment {
  root: string
  interpreter: string
  lock: string
  assertUnchanged: () => Promise<void>
  dispose: () => void
}

export class PythonDependencyError extends Error {
  constructor (readonly errors: string[]) {
    super(`Python dependency preparation failed:\n${errors.join('\n')}`)
    this.name = 'PythonDependencyError'
  }
}

export async function preparePythonEnvironment (
  tooling: PythonToolRuntime,
  runtime: ValidationRuntime,
  requirements: string,
  packages: Record<string, string>
): Promise<PythonEnvironment> {
  const root = mkdtempSync(path.join(tmpdir(), 'teams-python-validation-'))
  const request = path.join(root, 'request.json')
  const result = path.join(root, 'result.json')
  const environmentRoot = path.join(root, 'environment')
  const dispose = (): void => rmSync(root, {
    recursive: true,
    force: true,
  })
  writeFileSync(request, JSON.stringify({
    requirements,
    packages,
    environmentRoot,
  }))

  try {
    const errors = await runtime.runCommand(tooling.interpreter, [
      '-I', path.join(tooling.repo, 'automation/teams-sample-sync/scripts/python_prepare.py'),
      request, result,
    ], tooling.repo)

    if (errors.length) {
      throw new PythonDependencyError(errors)
    }

    const evidence = record(JSON.parse(readFileSync(result, 'utf8')), 'Python environment evidence')
    const interpreter = path.join(environmentRoot, process.platform === 'win32' ? 'Scripts/python.exe' : 'bin/python')
    const expectedInterpreter = process.platform === 'win32' ? path.toNamespacedPath(interpreter) : interpreter

    if (evidence.interpreter !== expectedInterpreter || typeof evidence.lock !== 'string' || evidence.lock.length === 0) {
      throw new SyncError('Python preparation returned mismatched environment evidence')
    }

    const fingerprint = async (): Promise<unknown> => {
      const evidenceFile = path.join(root, 'environment-digest.json')
      const failures = await runtime.runCommand(tooling.interpreter, [
        '-I', '-B', path.join(tooling.repo, 'automation/teams-sample-sync/scripts/python_environment_digest.py'),
        environmentRoot, evidenceFile,
      ], tooling.repo)

      if (failures.length) {
        throw new SyncError(`Python environment fingerprint failed:\n${failures.join('\n')}`)
      }

      return record(JSON.parse(readFileSync(evidenceFile, 'utf8')), 'Python environment fingerprint').digest
    }

    const originalDigest = await fingerprint()

    if (typeof originalDigest !== 'string' || !/^[a-f0-9]{64}$/.test(originalDigest)) {
      throw new SyncError('Python environment fingerprint is invalid')
    }

    return {
      root: environmentRoot,
      interpreter: expectedInterpreter,
      lock: evidence.lock,
      assertUnchanged: async () => {
        if (await fingerprint() !== originalDigest) {
          throw new SyncError('Python validation environment changed during candidate execution')
        }
      },
      dispose,
    }
  } catch (error) {
    dispose()
    throw error
  }
}

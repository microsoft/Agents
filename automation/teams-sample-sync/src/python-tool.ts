// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Runs repository-owned Python policies with isolated imports and bounded output. */
import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { SyncError } from './errors.js'
import { sanitizedChildEnvironment } from './validation-process.js'

export interface PythonToolRuntime {
  repo: string
  interpreter: string
}

export function runPythonTool (
  runtime: PythonToolRuntime,
  scriptName: 'python_sdk_policy.py' | 'python_project.py' | 'python_environment_digest.py',
  request: Record<string, unknown>
): unknown {
  const script = path.join(runtime.repo, 'automation/teams-sample-sync/scripts', scriptName)
  const result = spawnSync(runtime.interpreter, ['-I', '-B', script], {
    cwd: runtime.repo,
    env: sanitizedChildEnvironment(),
    input: JSON.stringify(request),
    encoding: 'utf8',
    timeout: 30_000,
    maxBuffer: 1024 * 1024,
    windowsHide: true,
  })

  if (result.error) {
    throw new SyncError(`Cannot execute trusted Python tool ${scriptName}: ${result.error.message}`)
  }

  if (result.status !== 0) {
    throw new SyncError(`Trusted Python tool ${scriptName} failed: ${result.stderr.trim()}`)
  }

  try {
    return JSON.parse(result.stdout)
  } catch {
    throw new SyncError(`Trusted Python tool ${scriptName} returned invalid JSON`)
  }
}

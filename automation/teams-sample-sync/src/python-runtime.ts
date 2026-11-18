// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Locate coordinator-owned tooling; never use a candidate-provided interpreter. */
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { PythonPolicyRuntime } from './python-versions.js'

export function pythonPolicyRuntime (): PythonPolicyRuntime {
  const repo = fileURLToPath(new URL('../../../', import.meta.url))

  return {
    repo,
    interpreter: process.env.TEAMS_SYNC_PYTHON ?? path.join(repo, '.sync/python-tools',
      process.platform === 'win32' ? 'Scripts/python.exe' : 'bin/python'),
    pythonVersion: '3.11.14',
  }
}

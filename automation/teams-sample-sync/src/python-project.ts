// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Inspects Python project declarations and source without importing candidate code. */
import { record, SyncError } from './config.js'
import { runPythonTool, type PythonToolRuntime } from './python-tool.js'
import { assertPythonSdkSelection, type PythonPolicyRuntime } from './python-versions.js'
import type { SourceCapabilities } from './language-validation.js'
import type { AgentsSdkVersionSelection } from './types.js'

interface PythonProjectEvidence {
  errors: string[]
  capabilities: SourceCapabilities
}

function strings (value: unknown, name: string): string[] {
  if (!Array.isArray(value) || value.some((entry) => typeof entry !== 'string')) {
    throw new SyncError(`Invalid Python project ${name} evidence`)
  }

  return value as string[]
}

export function inspectPythonProject (
  runtime: PythonToolRuntime,
  root: string,
  packages: Record<string, string>
): PythonProjectEvidence {
  const evidence = record(runPythonTool(runtime, 'python_project.py', {
    root,
    packages,
  }), 'Python project evidence')
  const capabilities = record(evidence.capabilities, 'Python source capabilities')

  if (typeof capabilities.bot !== 'boolean' || typeof capabilities.composeExtensions !== 'boolean') {
    throw new SyncError('Invalid Python capability flags')
  }

  for (const field of ['queryCommands', 'actionCommands']) {
    if (!Array.isArray(capabilities[field]) || capabilities[field].some((entry: unknown) => {
      const command = record(entry, 'Python command')

      return typeof command.id !== 'string' || typeof command.evidence !== 'string'
    })) {
      throw new SyncError(`Invalid Python ${field} evidence`)
    }
  }

  strings(capabilities.errors, 'capability errors')
  strings(capabilities.resourceSpecificPermissions, 'resource permissions')

  return {
    errors: strings(evidence.errors, 'errors'),
    capabilities: capabilities as unknown as SourceCapabilities,
  }
}

export function checkPythonProject (
  runtime: PythonPolicyRuntime,
  root: string,
  selection: AgentsSdkVersionSelection
): string[] {
  assertPythonSdkSelection(runtime, selection, selection.minimumAgentsSdkVersion)

  return inspectPythonProject(runtime, root, selection.packages!).errors
}

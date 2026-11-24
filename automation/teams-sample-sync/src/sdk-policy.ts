/** Dispatches trusted SDK policy without language branches in callers. */
import { assertAgentsSdkVersionSelection, resolveAgentsSdkVersion } from './versions.js'
import { assertJsSdkSelection, resolveJsSdkVersions } from './js-versions.js'
import { assertPythonSdkSelection, resolvePythonSdkVersions } from './python-versions.js'
import { pythonPolicyRuntime } from './python-runtime.js'
import { SyncError } from './errors.js'
import type { AgentsSdkVersionSelection, Targets } from './types.js'

export function assertSdkSelection (configured: Targets, selection: AgentsSdkVersionSelection): void {
  const minimum = configured.packagePolicy.minimumAgentsSdkVersion

  switch (configured.language) {
    case 'dotnet': return assertAgentsSdkVersionSelection(selection, minimum)
    case 'js': return assertJsSdkSelection(selection, minimum)
    case 'python': return assertPythonSdkSelection(pythonPolicyRuntime(), selection, minimum)
    default: throw new SyncError(`Unsupported SDK language: ${String(configured.language)}`)
  }
}

export async function resolveSdkSelection (configured: Targets): Promise<AgentsSdkVersionSelection> {
  const minimum = configured.packagePolicy.minimumAgentsSdkVersion

  switch (configured.language) {
    case 'dotnet': return resolveAgentsSdkVersion(minimum)
    case 'js': return resolveJsSdkVersions(minimum)
    case 'python': return resolvePythonSdkVersions(pythonPolicyRuntime(), minimum)
    default: throw new SyncError(`Unsupported SDK language: ${String(configured.language)}`)
  }
}

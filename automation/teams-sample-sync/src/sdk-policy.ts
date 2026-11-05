/** Dispatches trusted SDK policy without language branches in callers. */
import { assertAgentsSdkVersionSelection, resolveAgentsSdkVersion } from './versions.js'
import { assertJsSdkSelection, resolveJsSdkVersions } from './js-versions.js'
import type { AgentsSdkVersionSelection, Targets } from './types.js'

export function assertSdkSelection (configured: Targets, selection: AgentsSdkVersionSelection): void {
  const assert = configured.language === 'dotnet' ? assertAgentsSdkVersionSelection : assertJsSdkSelection
  assert(selection, configured.packagePolicy.minimumAgentsSdkVersion)
}

export async function resolveSdkSelection (configured: Targets): Promise<AgentsSdkVersionSelection> {
  const resolve = configured.language === 'dotnet' ? resolveAgentsSdkVersion : resolveJsSdkVersions

  return resolve(configured.packagePolicy.minimumAgentsSdkVersion)
}

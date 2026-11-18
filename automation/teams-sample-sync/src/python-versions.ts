// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Bounded bridge to the trusted PEP 440/508 SDK selection policy. */
import { SyncError } from './errors.js'
import { runPythonTool } from './python-tool.js'
import { record } from './config.js'
import type { AgentsSdkVersionSelection } from './types.js'

export interface PythonPolicyRuntime {
  repo: string
  interpreter: string
  pythonVersion: string
}

export const PYTHON_SDK_PACKAGES = [
  'microsoft-agents-activity',
  'microsoft-agents-hosting-core',
  'microsoft-agents-hosting-aiohttp',
  'microsoft-agents-authentication-msal',
  'microsoft-agents-hosting-msteams',
] as const

async function readMetadata (response: Response): Promise<Record<string, unknown>> {
  if (!response.body) {
    throw new SyncError('Python SDK metadata response has no body')
  }

  const reader = response.body.getReader()
  const chunks: Uint8Array[] = []
  let size = 0

  try {
    while (true) {
      const { done, value } = await reader.read()

      if (done) {
        break
      }

      size += value.byteLength

      if (size > 8 * 1024 * 1024) {
        await reader.cancel()
        throw new SyncError('Python SDK metadata exceeds its size limit')
      }

      chunks.push(value)
    }

    return record(JSON.parse(Buffer.concat(chunks).toString('utf8')), 'Python SDK metadata')
  } finally {
    reader.releaseLock()
  }
}

function runPolicy (runtime: PythonPolicyRuntime, request: Record<string, unknown>): unknown {
  return runPythonTool(runtime, 'python_sdk_policy.py', {
    ...request,
    pythonVersion: runtime.pythonVersion,
  })
}

export function assertPythonSdkSelection (
  runtime: PythonPolicyRuntime,
  selection: AgentsSdkVersionSelection,
  minimum: string
): void {
  runPolicy(runtime, {
    operation: 'assert',
    selection,
    minimum,
  })
}

export function selectPythonSdkVersions (
  runtime: PythonPolicyRuntime,
  minimum: string,
  feeds: Record<string, unknown>
): AgentsSdkVersionSelection {
  const selection = runPolicy(runtime, {
    operation: 'select',
    minimum,
    feeds,
  }) as AgentsSdkVersionSelection
  assertPythonSdkSelection(runtime, selection, minimum)

  return selection
}

/** Fetch release-specific dependency metadata; never execute package artifacts. */
export async function resolvePythonSdkVersions (
  runtime: PythonPolicyRuntime,
  minimum: string,
  request: typeof fetch = fetch
): Promise<AgentsSdkVersionSelection> {
  const deadline = AbortSignal.timeout(120_000)
  const metadata = async (url: string): Promise<Record<string, unknown>> => {
    const response = await request(url, {
      signal: AbortSignal.any([deadline, AbortSignal.timeout(20_000)]),
      redirect: 'error',
      headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
      throw new SyncError(`Cannot resolve Python SDK metadata: HTTP ${response.status}`)
    }

    return readMetadata(response)
  }

  const feeds: Record<string, unknown[]> = {}
  let requests = 0

  for (const name of PYTHON_SDK_PACKAGES) {
    const index = await metadata(`https://pypi.org/pypi/${name}/json`)
    const releases = record(index.releases, `${name}.releases`)
    feeds[name] = []

    for (const [version, artifacts] of Object.entries(releases)) {
      // Coarse major filtering only; packaging owns all version semantics.
      if (version.split('.')[0] !== minimum.split('.')[0] || !Array.isArray(artifacts) || artifacts.length === 0) {
        continue
      }

      requests += 1

      if (requests > 256) {
        throw new SyncError('Python SDK metadata resolution exceeded its request limit')
      }

      const release = await metadata(`https://pypi.org/pypi/${name}/${encodeURIComponent(version)}/json`)
      const info = record(release.info, `${name}@${version}.info`)
      feeds[name]!.push({
        name: info.name,
        version: info.version,
        requires_python: info.requires_python,
        requires_dist: info.requires_dist ?? [],
        yanked: info.yanked,
      })
    }
  }

  return selectPythonSdkVersions(runtime, minimum, feeds)
}

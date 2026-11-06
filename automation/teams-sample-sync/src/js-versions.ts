/** Selects a compatible stable npm package set and freezes exact versions. */
import semver from 'semver'
import { record, SyncError } from './config.js'
import type { AgentsSdkVersionSelection } from './types.js'

// Resolve the hosts first: their exact dependencies constrain the lower layers.
export const JS_SDK_PACKAGES = [
  '@microsoft/agents-hosting-express',
  '@microsoft/agents-hosting-extensions-msteams',
  '@microsoft/agents-hosting',
  '@microsoft/agents-activity',
] as const

interface PackageVersion {
  version: string
  dependencies: Record<string, string>
  peerDependencies: Record<string, string>
}

function stableVersion (value: string): boolean {
  return semver.valid(value) === value && semver.prerelease(value) === null && !value.includes('+')
}

function compatibleVersion (value: string, minimum: string): boolean {
  return stableVersion(value) && semver.major(value) === semver.major(minimum) && semver.gte(value, minimum)
}

function dependencyMap (raw: unknown, name: string): Record<string, string> {
  const dependencies = raw === undefined ? {} : record(raw, name)

  for (const [key, value] of Object.entries(dependencies)) {
    if (typeof value !== 'string') {
      throw new SyncError(`Invalid dependency range for ${key}`)
    }
  }

  return dependencies as Record<string, string>
}

function candidates (name: string, raw: unknown, minimum: string): PackageVersion[] {
  const feed = record(raw, `npm metadata for ${name}`)

  if (feed.name !== name) {
    throw new SyncError(`npm metadata name differs from ${name}`)
  }

  const versions = record(feed.versions, `${name}.versions`)
  const compatible: PackageVersion[] = []

  for (const [version, rawMetadata] of Object.entries(versions)) {
    if (!compatibleVersion(version, minimum)) {
      continue
    }

    const metadata = record(rawMetadata, `${name}@${version}`)

    if (metadata.version !== version || metadata.deprecated) {
      continue
    }

    const engines = metadata.engines === undefined ? {} : record(metadata.engines, `${name}.engines`)

    if (engines.node !== undefined && (typeof engines.node !== 'string' || !semver.satisfies('24.0.0', engines.node))) {
      continue
    }

    compatible.push({
      version,
      dependencies: dependencyMap(metadata.dependencies, `${name}.dependencies`),
      peerDependencies: dependencyMap(metadata.peerDependencies, `${name}.peerDependencies`),
    })
  }

  return compatible.sort((left, right) => semver.rcompare(left.version, right.version))
}

function compatibleSet (selected: Record<string, PackageVersion>): boolean {
  for (const metadata of Object.values(selected)) {
    for (const ranges of [metadata.dependencies, metadata.peerDependencies]) {
      for (const [name, range] of Object.entries(ranges)) {
        const installed = selected[name]

        if (installed && (!semver.validRange(range) || !semver.satisfies(installed.version, range))) {
          return false
        }
      }
    }
  }

  return true
}

export function selectJsSdkVersions (
  minimum: string,
  feeds: Record<string, unknown>
): AgentsSdkVersionSelection {
  if (!stableVersion(minimum)) {
    throw new SyncError('JS minimum Agents SDK version must be an exact stable version')
  }

  const available = Object.fromEntries(JS_SDK_PACKAGES.map((name) => [name, candidates(name, feeds[name], minimum)]))
  let attempts = 0
  const search = (position: number, selected: Record<string, PackageVersion>): Record<string, string> | undefined => {
    const name = JS_SDK_PACKAGES[position]

    if (!name) {
      return Object.fromEntries(Object.entries(selected).map(([key, value]) => [key, value.version]))
    }

    for (const candidate of available[name]!) {
      attempts += 1

      if (attempts > 50_000) {
        throw new SyncError('JS SDK compatibility search exceeded its supported limit')
      }

      const next = {
        ...selected,
        [name]: candidate
      }

      if (compatibleSet(next)) {
        const result = search(position + 1, next)

        if (result) {
          return result
        }
      }
    }

    return undefined
  }

  const packages = search(0, {})

  if (!packages) {
    throw new SyncError(`No compatible stable JS SDK package set at or above ${minimum} supports Node 24`)
  }

  const selectedAgentsSdkVersion = packages['@microsoft/agents-hosting']!

  return {
    minimumAgentsSdkVersion: minimum,
    selectedAgentsSdkVersion,
    releaseTag: `npm:${selectedAgentsSdkVersion}`,
    packages,
  }
}

export function assertJsSdkSelection (selection: AgentsSdkVersionSelection, minimum: string): void {
  if (
    !stableVersion(minimum) || selection.minimumAgentsSdkVersion !== minimum ||
    !selection.packages || Object.keys(selection.packages).length !== JS_SDK_PACKAGES.length ||
    JS_SDK_PACKAGES.some((name) => !compatibleVersion(selection.packages![name] ?? '', minimum)) ||
    selection.selectedAgentsSdkVersion !== selection.packages['@microsoft/agents-hosting'] ||
    selection.releaseTag !== `npm:${selection.selectedAgentsSdkVersion}`
  ) {
    throw new SyncError('JS SDK selection is incomplete or incompatible with the configured minimum')
  }
}

export async function resolveJsSdkVersions (
  minimum: string,
  request: typeof fetch = fetch
): Promise<AgentsSdkVersionSelection> {
  const feeds = await Promise.all(JS_SDK_PACKAGES.map(async (name) => {
    const response = await request(`https://registry.npmjs.org/${encodeURIComponent(name)}`, {
      headers: {
        Accept: 'application/vnd.npm.install-v1+json',
        'User-Agent': 'teams-sample-sync'
      },
      signal: AbortSignal.timeout(20_000),
      redirect: 'error',
    })

    if (!response.ok) {
      throw new SyncError(`Cannot resolve ${name} from npm: HTTP ${response.status}`)
    }

    return [name, await response.json()] as const
  }))

  return selectJsSdkVersions(minimum, Object.fromEntries(feeds))
}

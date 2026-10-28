/** Resolves a stable Agents SDK release line for a frozen synchronization plan. */
import { SyncError } from './config.js'
import type { AgentsSdkVersionSelection } from './types.js'

const RELEASE_URL = 'https://api.github.com/repos/microsoft/Agents-for-net/releases'
const PACKAGE_IDS = [
  'microsoft.agents.hosting.aspnetcore',
  'microsoft.agents.authentication.msal',
  'microsoft.agents.extensions.msteams',
  // Contract tests must restore on the same line as migrated samples.
  'microsoft.agents.builder.testing',
] as const

interface VersionLine {
  major: number
  minor: number
}

export function parseVersionLine (value: string): VersionLine | undefined {
  const match = /^(\d+)\.(\d+)(?:\.(?:\d+|\*))?$/.exec(value)
  if (!match) {
    return undefined
  }
  return { major: Number(match[1]), minor: Number(match[2]) }
}

export function compareVersionLines (left: VersionLine, right: VersionLine): number {
  return left.major - right.major || left.minor - right.minor
}

export function assertAgentsSdkVersionSelection (selection: AgentsSdkVersionSelection, configuredMinimum: string): void {
  const minimum = parseVersionLine(configuredMinimum)
  const selected = parseVersionLine(selection.selectedAgentsSdkVersion)
  const release = /^v(\d+)\.(\d+)\.\d+$/.exec(selection.releaseTag)
  if (
    selection.minimumAgentsSdkVersion !== configuredMinimum ||
    !/^\d+\.\d+\.\*$/.test(selection.selectedAgentsSdkVersion) ||
    !minimum ||
    !selected ||
    !release ||
    selected.major !== minimum.major ||
    Number(release[1]) !== minimum.major ||
    compareVersionLines(selected, minimum) < 0 ||
    compareVersionLines(selected, { major: Number(release[1]), minor: Number(release[2]) }) > 0
  ) {
    throw new SyncError('Agents SDK version selection is incompatible with the configured minimum or release')
  }
}

export function selectAgentsSdkVersion (
  minimumAgentsSdkVersion: string,
  releases: unknown,
  packageIndexes: unknown[]
): AgentsSdkVersionSelection {
  const minimum = /^\d+\.\d+$/.test(minimumAgentsSdkVersion) ? parseVersionLine(minimumAgentsSdkVersion) : undefined
  if (!minimum) {
    throw new SyncError('Minimum Agents SDK version must be a major.minor line')
  }

  if (!Array.isArray(releases)) {
    throw new SyncError('Invalid Agents SDK GitHub release feed')
  }
  const candidates = releases
    .flatMap((value): Array<{ tag: string; major: number; minor: number; patch: number }> => {
      if (!value || typeof value !== 'object') {
        return []
      }
      const release = value as { tag_name?: unknown; prerelease?: unknown; draft?: unknown }
      const match = typeof release.tag_name === 'string' ? /^v(\d+)\.(\d+)\.(\d+)$/.exec(release.tag_name) : null
      if (!match || release.prerelease !== false || release.draft !== false) {
        return []
      }
      const major = Number(match[1])
      const minor = Number(match[2])
      if (major !== minimum.major || compareVersionLines({ major, minor }, minimum) < 0) {
        return []
      }
      return [{ tag: release.tag_name as string, major, minor, patch: Number(match[3]) }]
    })
    .sort((left, right) => right.major - left.major || right.minor - left.minor || right.patch - left.patch)
  const release = candidates[0]
  if (!release) {
    throw new SyncError(`No stable Agents SDK release in major ${minimum.major} at or above ${minimumAgentsSdkVersion}`)
  }
  const latest = { major: release.major, minor: release.minor }
  if (packageIndexes.length !== PACKAGE_IDS.length) {
    throw new SyncError('Agents SDK package version feeds are incomplete')
  }

  const available = packageIndexes.map((index, position) => {
    const versions = (index as { versions?: unknown } | null)?.versions
    if (!Array.isArray(versions)) {
      throw new SyncError(`Invalid NuGet version feed for ${PACKAGE_IDS[position]}`)
    }
    return new Set(
      versions.flatMap((value): string[] => {
        if (typeof value !== 'string' || !/^\d+\.\d+\.\d+$/.test(value)) {
          return []
        }
        const line = parseVersionLine(value)!
        if (
          line.major !== minimum.major ||
          compareVersionLines(line, minimum) < 0 ||
          compareVersionLines(line, latest) > 0
        ) {
          return []
        }
        return [`${line.major}.${line.minor}`]
      })
    )
  })
  const shared = [...available[0]!]
    .filter((line) => available.every((versions) => versions.has(line)))
    .sort((left, right) => compareVersionLines(parseVersionLine(right)!, parseVersionLine(left)!))
  if (!shared[0]) {
    throw new SyncError(
      `No stable Agents SDK release line at or above ${minimumAgentsSdkVersion} has all required packages`
    )
  }

  return {
    minimumAgentsSdkVersion,
    selectedAgentsSdkVersion: `${shared[0]}.*`,
    releaseTag: release.tag,
  }
}

export async function resolveAgentsSdkVersion (
  minimumAgentsSdkVersion: string,
  request: typeof fetch = fetch
): Promise<AgentsSdkVersionSelection> {
  const githubHeaders: Record<string, string> = {
    Accept: 'application/vnd.github+json',
    'User-Agent': 'teams-sample-sync',
  }
  if (process.env.GITHUB_TOKEN) {
    githubHeaders.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`
  }
  const fetchJson = async (url: string, github = false): Promise<unknown> => {
    const response = await request(url, {
      ...(github ? { headers: githubHeaders } : {}),
      signal: AbortSignal.timeout(10_000),
    })
    if (!response.ok) {
      throw new SyncError(`Cannot resolve Agents SDK versions from ${url}: HTTP ${response.status}`)
    }
    return response.json() as Promise<unknown>
  }
  const packageIndexesPromise = Promise.all(
    PACKAGE_IDS.map((id) => fetchJson(`https://api.nuget.org/v3-flatcontainer/${id}/index.json`))
  )
  const releases: unknown[] = []
  for (let page = 1; page <= 20; page++) {
    const batch = await fetchJson(`${RELEASE_URL}?per_page=100&page=${page}`, true)
    if (!Array.isArray(batch)) {
      throw new SyncError('Invalid Agents SDK GitHub release feed')
    }
    releases.push(...batch)
    if (batch.length < 100) {
      break
    }
    if (page === 20) {
      throw new SyncError('Agents SDK release history exceeds the supported page limit')
    }
  }
  return selectAgentsSdkVersion(minimumAgentsSdkVersion, releases, await packageIndexesPromise)
}

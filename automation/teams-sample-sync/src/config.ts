/** Loads synchronization policy and rejects unsafe paths or conflicting destinations. */
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { parse } from 'yaml'
import { language, type Language } from './identity.js'
import { SyncError } from './errors.js'
import type {
  Configuration, LanguageProfile, ManifestTarget, Protection, Target, Targets
} from './types.js'

export { SyncError } from './errors.js'

export const CONFIG_DIRECTORY = 'automation/teams-sample-sync/config'

const EXACT_VERSION = /^\d+\.\d+\.\d+$/
const DOTNET_VERSION_LINE = /^\d+\.\d+$/
const EXACT_HOSTNAME = /^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$/

export function record (value: unknown, name: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new SyncError(`${name} must be a mapping`)
  }

  return value as Record<string, unknown>
}

export function text (value: unknown, name: string): string {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new SyncError(`${name} is required`)
  }

  return value
}

function stringList (value: unknown, name: string): string[] {
  if (!Array.isArray(value) || value.some((item) => typeof item !== 'string' || item === '')) {
    throw new SyncError(`${name} must be a string list`)
  }

  return value as string[]
}

export function relativePath (value: string, allowDot = false): boolean {
  const normalized = value.replaceAll('\\', '/')

  if (value === '' || path.posix.isAbsolute(normalized) || path.win32.isAbsolute(value)) {
    return false
  }

  for (const segment of normalized.split('/')) {
    if (segment === '..') {
      return false
    }

    if (!allowDot && (segment === '' || segment === '.')) {
      return false
    }
  }

  return true
}

export function containedPath (root: string, relative: string): string {
  if (!relativePath(relative)) {
    throw new SyncError(`Unsafe repository path: ${relative}`)
  }

  const candidate = path.resolve(root, relative)
  const pathFromRoot = path.relative(root, candidate)

  if (pathFromRoot.startsWith(`..${path.sep}`) || path.isAbsolute(pathFromRoot)) {
    throw new SyncError(`Path is outside repository: ${relative}`)
  }

  return candidate
}

export function yaml (file: string): Record<string, unknown> {
  try {
    return record(parse(readFileSync(file, 'utf8')), file)
  } catch (error) {
    throw new SyncError(`Cannot read YAML ${file}: ${error instanceof Error ? error.message : String(error)}`)
  }
}

function parseManifest (value: unknown, name: string): ManifestTarget {
  const item = record(value, name)
  const result: ManifestTarget = {
    distribution: text(item.distribution, `${name}.distribution`),
    packageDirectory: text(item.packageDirectory, `${name}.packageDirectory`),
    placeholderConvention: text(item.placeholderConvention, `${name}.placeholderConvention`),
  }

  if (!relativePath(result.packageDirectory)) {
    throw new SyncError(`${name}.packageDirectory is unsafe`)
  }

  if (item.expectedLinkDomains !== undefined) {
    const domains = stringList(item.expectedLinkDomains, `${name}.expectedLinkDomains`)
    const hasInvalidHostname = domains.some((domain) => !EXACT_HOSTNAME.test(domain))
    const hasDuplicates = new Set(domains).size !== domains.length

    if (domains.length === 0 || hasInvalidHostname || hasDuplicates) {
      throw new SyncError(`${name}.expectedLinkDomains must contain unique exact lowercase hostnames`)
    }

    result.expectedLinkDomains = domains
  }

  return result
}

function parseSampleTarget (raw: unknown, profileName: Language, sampleName: string): Target {
  if (!/^[a-z0-9][a-z0-9-]*$/.test(sampleName)) {
    throw new SyncError(`Invalid sample name: ${sampleName}`)
  }

  const value = record(raw, `${profileName}/${sampleName}`)
  const source = text(value.source, `${sampleName}.source`)
  const destination = text(value.destination, `${sampleName}.destination`)

  if (!relativePath(source) || !relativePath(destination)) {
    throw new SyncError(`Sample ${sampleName} has unsafe path`)
  }

  return {
    source,
    destination,
    manifest: parseManifest(value.manifest, `${sampleName}.manifest`),
  }
}

function parseProfile (raw: unknown, name: Language): LanguageProfile {
  const value = record(raw, `profile ${name}`)
  const policy = record(value.packagePolicy, `${name}.packagePolicy`)
  const rawSamples = record(value.samples, `${name}.samples`)

  if (Object.keys(rawSamples).length === 0) {
    throw new SyncError(`Profile ${name} must select at least one sample`)
  }

  const samples: Record<string, Target> = {}

  for (const [sample, rawTarget] of Object.entries(rawSamples)) {
    samples[sample] = parseSampleTarget(rawTarget, name, sample)
  }

  const packagePolicy: LanguageProfile['packagePolicy'] = { minimumAgentsSdkVersion: text(policy.minimumAgentsSdkVersion, `${name}.minimumAgentsSdkVersion`), }

  if (name === 'dotnet') {
    packagePolicy.targetFramework = text(policy.targetFramework, `${name}.targetFramework`)
  } else {
    packagePolicy.nodeVersion = text(policy.nodeVersion, `${name}.nodeVersion`)
  }

  const result: LanguageProfile = {
    destinationRoot: text(value.destinationRoot, `${name}.destinationRoot`),
    canonicalSample: text(value.canonicalSample, `${name}.canonicalSample`),
    manifestAssets: text(value.manifestAssets, `${name}.manifestAssets`),
    migrationSkill: text(value.migrationSkill, `${name}.migrationSkill`),
    syncSkill: text(value.syncSkill, `${name}.syncSkill`),
    contractDirectory: text(value.contractDirectory, `${name}.contractDirectory`),
    outputDigestExcludes: stringList(value.outputDigestExcludes, `${name}.outputDigestExcludes`),
    packagePolicy,
    samples,
  }

  const minimumPattern = name === 'dotnet' ? DOTNET_VERSION_LINE : EXACT_VERSION

  if (!minimumPattern.test(result.packagePolicy.minimumAgentsSdkVersion)) {
    throw new SyncError(`Invalid minimum Agents SDK version for ${name}`)
  }

  if (name === 'js' && result.packagePolicy.nodeVersion !== '24') {
    throw new SyncError('JS migration runtime must use Node 24')
  }

  for (const [key, candidate] of Object.entries(result)) {
    if (typeof candidate === 'string' && !relativePath(candidate)) {
      throw new SyncError(`Profile ${name} has unsafe ${key}`)
    }
  }

  return result
}

function assertDistinctDestinations (profiles: Configuration['profiles']): void {
  const destinations: string[] = []

  for (const profile of Object.values(profiles)) {
    for (const target of Object.values(profile.samples)) {
      // Compare normalized, case-insensitive paths so Windows checkouts are safe too.
      const destinationRoot = profile.destinationRoot.replaceAll('\\', '/')
      const sampleDirectory = target.destination.replaceAll('\\', '/')
      const destination = path.posix.join(destinationRoot, sampleDirectory).toLowerCase()
      const overlaps = destinations.some((existing) =>
        destination === existing ||
        destination.startsWith(existing + '/') ||
        existing.startsWith(destination + '/')
      )

      if (overlaps) {
        throw new SyncError(`Overlapping destination: ${destination}`)
      }

      destinations.push(destination)
    }
  }
}

/** Reads all profiles and validates destinations across languages, not just within one profile. */
export function configuration (repo: string): Configuration {
  const value = yaml(path.join(repo, CONFIG_DIRECTORY, 'targets.yml'))

  if (value.version !== 2) {
    throw new SyncError('targets.yml must use version 2')
  }

  const upstream = record(value.upstream, 'upstream')
  const copilot = record(value.copilot, 'copilot')
  const profiles: Configuration['profiles'] = {}

  for (const [name, raw] of Object.entries(record(value.profiles, 'profiles'))) {
    const profileLanguage = language(name)
    profiles[profileLanguage] = parseProfile(raw, profileLanguage)
  }

  if (Object.keys(profiles).length === 0) {
    throw new SyncError('At least one language profile is required')
  }

  const result: Configuration = {
    version: 2,
    upstream: {
      repository: text(process.env.TEAMS_SAMPLES_REPOSITORY ?? upstream.repository, 'upstream.repository'),
      ref: text(upstream.ref, 'upstream.ref'),
      root: text(upstream.root, 'upstream.root'),
    },
    manifestSkill: text(value.manifestSkill, 'manifestSkill'),
    copilot: {
      sdkVersion: text(copilot.sdkVersion, 'copilot.sdkVersion'),
      runtimeVersion: text(copilot.runtimeVersion, 'copilot.runtimeVersion'),
    },
    validatorVersion: text(value.validatorVersion, 'validatorVersion'),
    profiles,
  }

  if (!EXACT_VERSION.test(result.copilot.sdkVersion) || !EXACT_VERSION.test(result.copilot.runtimeVersion)) {
    throw new SyncError('Copilot SDK and runtime versions must be exact versions')
  }

  if (!relativePath(result.upstream.root) || !relativePath(result.manifestSkill)) {
    throw new SyncError('Unsafe upstream root or manifest skill')
  }

  assertDistinctDestinations(profiles)

  return result
}

/** Resolves one language profile together with the shared synchronization settings. */
export function targets (repo: string, selectedLanguage: Language = 'dotnet'): Targets {
  const configured = configuration(repo)
  const selected = configured.profiles[selectedLanguage]

  if (!selected) {
    throw new SyncError(`Language is not configured: ${selectedLanguage}`)
  }

  const { profiles: _profiles, ...shared } = configured

  return {
    ...shared,
    ...selected,
    language: selectedLanguage
  }
}

export function selectedLanguages (repo: string, selected = 'all'): Language[] {
  const configured = configuration(repo)

  if (selected === 'all') {
    return Object.keys(configured.profiles).map(language).sort()
  }

  const chosen = language(selected)

  if (!configured.profiles[chosen]) {
    throw new SyncError(`Language is not configured: ${selected}`)
  }

  return [chosen]
}

export function targetProtection (repo: string, configured: Targets): Protection {
  const shared = protection(repo)

  return {
    ...shared,
    outputDigestExcludes: [
      ...shared.outputDigestExcludes,
      ...configured.outputDigestExcludes,
    ],
  }
}

export function protection (repo: string): Protection {
  const value = yaml(path.join(repo, CONFIG_DIRECTORY, 'ownership.yml'))

  if (value.version !== 1) {
    throw new SyncError('ownership.yml must use version 1')
  }

  return {
    version: 1,
    protectedPaths: stringList(value.protectedPaths, 'ownership.yml protectedPaths'),
    outputDigestExcludes: stringList(value.outputDigestExcludes, 'ownership.yml outputDigestExcludes'),
  }
}

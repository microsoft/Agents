/** For Copilot Agents only: migration policy-loading infrastructure. */
/**
 * Loads the target and ownership YAML files into validated runtime configuration.
 * This is the policy boundary for upstream locations, destination directories, SDK versions, and protected
 * paths; it fails early on malformed values or paths that could escape the repository.
 */
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { parse } from 'yaml'
import type { ManifestTarget, Protection, Target, Targets } from './types.js'

export const CONFIG_DIRECTORY = 'automation/teams-sample-sync/config'
export class SyncError extends Error { constructor (message: string) { super(message); this.name = 'SyncError' } }
export function record (value: unknown, name: string): Record<string, unknown> { if (!value || typeof value !== 'object' || Array.isArray(value)) throw new SyncError(`${name} must be a mapping`); return value as Record<string, unknown> }
export function text (value: unknown, name: string): string { if (typeof value !== 'string' || value.trim() === '') throw new SyncError(`${name} is required`); return value }
function stringList (value: unknown, name: string): string[] { if (!Array.isArray(value) || value.some((item) => typeof item !== 'string' || item === '')) throw new SyncError(`${name} must be a string list`); return value as string[] }
export function relativePath (value: string, allowDot = false): boolean { const normalized = value.replaceAll('\\', '/'); return value !== '' && !path.posix.isAbsolute(normalized) && !path.win32.isAbsolute(value) && normalized.split('/').every((part) => part !== '..' && (allowDot || (part !== '' && part !== '.'))) }
export function containedPath (root: string, relative: string): string { if (!relativePath(relative)) throw new SyncError(`Unsafe repository path: ${relative}`); const candidate = path.resolve(root, relative); const back = path.relative(root, candidate); if (back.startsWith(`..${path.sep}`) || path.isAbsolute(back)) throw new SyncError(`Path is outside repository: ${relative}`); return candidate }
export function yaml (file: string): Record<string, unknown> { try { return record(parse(readFileSync(file, 'utf8')), file) } catch (error) { throw new SyncError(`Cannot read YAML ${file}: ${error instanceof Error ? error.message : String(error)}`) } }
function manifest (value: unknown, name: string): ManifestTarget { const item = record(value, name); const result = { distribution: text(item.distribution, `${name}.distribution`), packageDirectory: text(item.packageDirectory, `${name}.packageDirectory`), placeholderConvention: text(item.placeholderConvention, `${name}.placeholderConvention`) }; if (!relativePath(result.packageDirectory)) throw new SyncError(`${name}.packageDirectory is unsafe`); return result }

export function targets (repo: string): Targets {
  const value = yaml(path.join(repo, CONFIG_DIRECTORY, 'targets.yml'))
  if (value.version !== 1) throw new SyncError('targets.yml must use version 1')
  const upstream = record(value.upstream, 'targets.yml upstream'); const copilot = record(value.copilot, 'targets.yml copilot'); const packagePolicy = record(value.packagePolicy, 'targets.yml packagePolicy'); const rawSamples = record(value.samples, 'targets.yml samples')
  if (Object.keys(rawSamples).length === 0) throw new SyncError('targets.yml must select at least one sample')
  const samples: Record<string, Target> = {}; const destinations = new Set<string>()
  for (const [name, raw] of Object.entries(rawSamples)) { if (!/^[a-z0-9][a-z0-9-]*$/.test(name)) throw new SyncError(`Invalid sample name: ${name}`); const item = record(raw, `sample ${name}`); const source = text(item.source, `${name}.source`); const destination = text(item.destination, `${name}.destination`); if (!relativePath(source) || !relativePath(destination)) throw new SyncError(`Sample ${name} has unsafe path`); if (destinations.has(destination)) throw new SyncError(`Duplicate destination: ${destination}`); destinations.add(destination); samples[name] = { source, destination, manifest: manifest(item.manifest, `${name}.manifest`) } }
  const result: Targets = { version: 1, upstream: { repository: text(process.env.TEAMS_SAMPLES_REPOSITORY ?? upstream.repository, 'upstream.repository'), ref: text(upstream.ref, 'upstream.ref'), root: text(upstream.root, 'upstream.root') }, destinationRoot: text(value.destinationRoot, 'destinationRoot'), canonicalSample: text(value.canonicalSample, 'canonicalSample'), migrationSkill: text(value.migrationSkill, 'migrationSkill'), manifestSkill: text(value.manifestSkill, 'manifestSkill'), copilot: { sdkVersion: text(copilot.sdkVersion, 'copilot.sdkVersion'), runtimeVersion: text(copilot.runtimeVersion, 'copilot.runtimeVersion') }, packagePolicy: { targetFramework: text(packagePolicy.targetFramework, 'packagePolicy.targetFramework'), agentsSdkVersion: text(packagePolicy.agentsSdkVersion, 'packagePolicy.agentsSdkVersion') }, validatorVersion: text(value.validatorVersion, 'validatorVersion'), samples }
  if (!/^\d+\.\d+\.\d+$/.test(result.copilot.sdkVersion) || !/^\d+\.\d+\.\d+$/.test(result.copilot.runtimeVersion)) throw new SyncError('Copilot SDK and runtime versions must be exact versions')
  for (const [name, candidate] of Object.entries({ upstreamRoot: result.upstream.root, destinationRoot: result.destinationRoot, canonicalSample: result.canonicalSample, migrationSkill: result.migrationSkill, manifestSkill: result.manifestSkill })) if (!relativePath(candidate)) throw new SyncError(`targets.yml has unsafe ${name}`)
  return result
}
export function protection (repo: string): Protection { const value = yaml(path.join(repo, CONFIG_DIRECTORY, 'ownership.yml')); if (value.version !== 1) throw new SyncError('ownership.yml must use version 1'); return { version: 1, protectedPaths: stringList(value.protectedPaths, 'ownership.yml protectedPaths'), outputDigestExcludes: stringList(value.outputDigestExcludes, 'ownership.yml outputDigestExcludes') } }

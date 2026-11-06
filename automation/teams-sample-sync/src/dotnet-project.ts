/** .NET project and restored-package policy checks. */
import {
  existsSync,
  readdirSync,
  readFileSync,
  statSync
} from 'node:fs'
import path from 'node:path'
import { XMLParser } from 'fast-xml-parser'
import { SyncError } from './config.js'
import { compareVersionLines, parseVersionLine } from './versions.js'
import { sourceFiles } from './source-files.js'
import type { Targets } from './types.js'

const REQUIRED_PACKAGES = [
  'Microsoft.Agents.Authentication.Msal',
  'Microsoft.Agents.Hosting.AspNetCore',
  'Microsoft.Agents.Extensions.MSTeams',
] as const

function valuesByKey (value: unknown, key: string): unknown[] {
  if (Array.isArray(value)) {
    return value.flatMap((item) => valuesByKey(item, key))
  }

  if (!value || typeof value !== 'object') {
    return []
  }

  const matches: unknown[] = []

  for (const [name, child] of Object.entries(value as Record<string, unknown>)) {
    if (name === key) {
      matches.push(...(Array.isArray(child) ? child : [child]))
    }

    matches.push(...valuesByKey(child, key))
  }

  return matches
}

function scalar (value: unknown): string | undefined {
  if (['string', 'number', 'boolean'].includes(typeof value)) {
    return String(value)
  }

  if (value && typeof value === 'object' && typeof (value as Record<string, unknown>)['#text'] === 'string') {
    return (value as Record<string, unknown>)['#text'] as string
  }

  return undefined
}

export function checkProject (
  sampleRoot: string,
  configured: Targets,
  selectedAgentsSdkVersion: string
): {
    project?: string
    errors: string[]
  } {
  const errors: string[] = []
  const minimum = parseVersionLine(configured.packagePolicy.minimumAgentsSdkVersion)
  const selected = parseVersionLine(selectedAgentsSdkVersion)

  if (!minimum || !selected || selected.major !== minimum.major || compareVersionLines(selected, minimum) < 0) {
    throw new SyncError('Planned Agents SDK release line is below or incompatible with the configured minimum')
  }

  if (!existsSync(sampleRoot) || !statSync(sampleRoot).isDirectory()) {
    return { errors: ['Selected sample does not exist'] }
  }

  const projects = readdirSync(sampleRoot)
    .filter((name) => name.endsWith('.csproj'))
    .sort()

  if (projects.length !== 1) {
    return { errors: [`Expected one project file, found ${projects.length}`] }
  }

  const project = path.join(sampleRoot, projects[0]!)
  let xml: unknown

  try {
    xml = new XMLParser({
      ignoreAttributes: false,
      attributeNamePrefix: ''
    }).parse(readFileSync(project, 'utf8'))
  } catch (error) {
    return {
      project,
      errors: [`Invalid project XML: ${error instanceof Error ? error.message : String(error)}`]
    }
  }

  const framework = scalar(valuesByKey(xml, 'TargetFramework')[0])

  if (framework !== configured.packagePolicy.targetFramework) {
    errors.push(`TargetFramework must be ${configured.packagePolicy.targetFramework}, found ${String(framework)}`)
  }

  const packages = new Map<string, string | undefined>()

  for (const item of valuesByKey(xml, 'PackageReference')) {
    if (!item || typeof item !== 'object') {
      continue
    }

    const reference = item as Record<string, unknown>
    const name = scalar(reference.Include)

    if (name) {
      packages.set(name, scalar(reference.Version))
    }
  }

  let commonLine: string | undefined

  for (const packageName of REQUIRED_PACKAGES) {
    const requested = packages.get(packageName)
    const line = requested && /^\d+\.\d+\.(?:\d+|\*)$/.test(requested) ? parseVersionLine(requested) : undefined

    if (
      !line ||
      line.major !== minimum.major ||
      compareVersionLines(line, minimum) < 0 ||
      compareVersionLines(line, selected) < 0
    ) {
      errors.push(
        `${packageName} must use a stable ${minimum.major}.x version at or above the planned ${selectedAgentsSdkVersion} line`
      )
      continue
    }

    const value = `${line.major}.${line.minor}`

    if (commonLine && value !== commonLine) {
      errors.push(`${packageName} must use the same major.minor line as the other Agents SDK packages`)
    }

    commonLine ??= value
  }

  const legacyPackages = [...packages.keys()].filter(
    (name) =>
      name.startsWith('Microsoft.Bot.') ||
      name.startsWith('Microsoft.TeamsFx') ||
      name.startsWith('Microsoft.Teams.Plugins.') ||
      name.startsWith('Microsoft.Teams.Apps') ||
      name === 'Microsoft.Teams.Api'
  )

  if (legacyPackages.length > 0) {
    errors.push(`Legacy Teams or Bot SDK packages remain: ${legacyPackages.sort().join(', ')}`)
  }

  const sources = sourceFiles(sampleRoot)
    .filter(
      (file) =>
        file.endsWith('.cs') && !['bin', 'obj', 'tests'].includes(path.relative(sampleRoot, file).split(path.sep)[0]!)
    )
    .map((file) => readFileSync(file, 'utf8'))
    .join('\n')

  if (!sources.includes('AgentApplication')) {
    errors.push('Missing AgentApplication implementation')
  }

  if (!/partial\s+class\s+\w+[\s\S]*?:\s*AgentApplication/.test(sources)) {
    errors.push('Missing partial AgentApplication subclass')
  }

  if ((sources.match(/\[TeamsExtension\]/g) ?? []).length !== 1) {
    errors.push('Expected exactly one [TeamsExtension] attribute')
  }

  if (/AddTeams\s*\(|UseTeams\s*\(|Microsoft\.TeamsFx|Microsoft\.Bot\.Builder/.test(sources)) {
    errors.push('Teams SDK bootstrap or package usage remains')
  }

  const programPath = path.join(sampleRoot, 'Program.cs')

  if (!existsSync(programPath)) {
    errors.push('Missing Program.cs')
  } else {
    const program = readFileSync(programPath, 'utf8')

    for (const required of ['AddAgentDefaults', 'AddAgent<', 'UseAgents', 'MapDefaultAgentEndpoints']) {
      if (!program.includes(required)) {
        errors.push(`Program.cs is missing Agents host call: ${required}`)
      }
    }
  }

  if (sourceFiles(sampleRoot).some((file) => path.basename(file).toLowerCase() === 'manifest-evidence.md')) {
    errors.push('manifest-evidence.md is prohibited')
  }

  return {
    project,
    errors
  }
}

export function checkRestoredPackages (project: string, selectedAgentsSdkVersion: string): string[] {
  const selected = parseVersionLine(selectedAgentsSdkVersion)

  if (!selected) {
    throw new SyncError('Invalid planned Agents SDK release line')
  }

  const assetsPath = path.join(path.dirname(project), 'obj', 'project.assets.json')
  let targets: unknown

  try {
    targets = (JSON.parse(readFileSync(assetsPath, 'utf8')) as { targets?: unknown }).targets
  } catch (error) {
    return [`Cannot read restored package assets: ${error instanceof Error ? error.message : String(error)}`]
  }

  if (!targets || typeof targets !== 'object' || Array.isArray(targets) || Object.keys(targets).length === 0) {
    return ['Restored package assets contain no target frameworks']
  }

  const errors: string[] = []

  for (const [framework, libraries] of Object.entries(targets)) {
    if (!libraries || typeof libraries !== 'object' || Array.isArray(libraries)) {
      errors.push(`Restored package assets for ${framework} are invalid`)
      continue
    }

    const resolved = Object.keys(libraries)
    let commonLine: string | undefined

    for (const packageName of REQUIRED_PACKAGES) {
      const matches = resolved.filter((name) => name.toLowerCase().startsWith(`${packageName.toLowerCase()}/`))

      if (matches.length !== 1) {
        errors.push(`${packageName} must resolve exactly once for ${framework}`)
        continue
      }

      const version = matches[0]!.slice(packageName.length + 1)
      const line = /^\d+\.\d+\.\d+$/.test(version) ? parseVersionLine(version) : undefined

      if (!line || line.major !== selected.major || compareVersionLines(line, selected) < 0) {
        errors.push(
          `${packageName} resolved to ${version} for ${framework}, below or incompatible with planned ${selectedAgentsSdkVersion}`
        )
        continue
      }

      const value = `${line.major}.${line.minor}`

      if (commonLine && value !== commonLine) {
        errors.push(`${packageName} resolved on a different major.minor line for ${framework}`)
      }

      commonLine ??= value
    }
  }

  return errors
}

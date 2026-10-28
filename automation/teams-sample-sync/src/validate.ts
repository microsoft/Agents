/** For Copilot Agents only: deterministic publish-gate infrastructure. */
/**
 * Executes the deterministic validation gate for a migrated sample.
 * It checks project/package policy, manifest schema and capabilities, README claims, contract tests, builds,
 * and smoke tests; it returns machine-readable evidence that decides whether a generated patch can publish.
 */
import { spawn, spawnSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs'
import path from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { Ajv, type AnySchema, type ErrorObject } from 'ajv'
import { XMLParser } from 'fast-xml-parser'
import { parseDocument } from 'yaml'
import { digestDirectory } from './git.js'
import { SyncError } from './config.js'
import { compareVersionLines, parseVersionLine } from './versions.js'
import type { ManifestTarget, Targets, ValidationCheck, ValidationResult } from './types.js'

const REQUIRED_PACKAGES = [
  'Microsoft.Agents.Authentication.Msal',
  'Microsoft.Agents.Hosting.AspNetCore',
  'Microsoft.Agents.Extensions.MSTeams',
] as const

function allFiles (root: string): string[] {
  if (!existsSync(root)) {
    return []
  }
  const files: string[] = []
  const visit = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const item = path.join(directory, entry.name)
      if (entry.isSymbolicLink()) {
        throw new SyncError(`Symlink is not allowed: ${item}`)
      }
      if (entry.isDirectory() && !['bin', 'obj', '.git', '.vs'].includes(entry.name)) {
        visit(item)
      } else if (entry.isFile()) {
        files.push(item)
      }
    }
  }
  visit(root)
  return files
}

function valuesByKey (value: unknown, key: string): unknown[] {
  if (Array.isArray(value)) {
    return value.flatMap((item) => valuesByKey(item, key))
  }
  if (!value || typeof value !== 'object') {
    return []
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([name, child]) => [
    ...(name === key ? (Array.isArray(child) ? child : [child]) : []),
    ...valuesByKey(child, key),
  ])
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
): { project?: string; errors: string[] } {
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
    xml = new XMLParser({ ignoreAttributes: false, attributeNamePrefix: '' }).parse(readFileSync(project, 'utf8'))
  } catch (error) {
    return { project, errors: [`Invalid project XML: ${error instanceof Error ? error.message : String(error)}`] }
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
  const sources = allFiles(sampleRoot)
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
  if (allFiles(sampleRoot).some((file) => path.basename(file).toLowerCase() === 'manifest-evidence.md')) {
    errors.push('manifest-evidence.md is prohibited')
  }
  return { project, errors }
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

function renderPlaceholders (value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(renderPlaceholders)
  }
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, renderPlaceholders(item)])
    )
  }
  if (typeof value !== 'string') {
    return value
  }
  const replace = (_match: string, raw: string): string => {
    const name = raw.toUpperCase()
    if (name.includes('ID')) {
      return '00000000-0000-4000-8000-000000000000'
    }
    if (name.includes('DOMAIN') || name.includes('HOST')) {
      return 'example.com'
    }
    if (name.includes('URL')) {
      return 'https://example.com'
    }
    return 'placeholder'
  }
  return value.replace(/\$\{\{([^{}]+)\}\}/g, replace).replace(/<<([^<>]+)>>/g, replace)
}

interface ReadmeCommandClaim {
  title: string
  triggers: string[]
}

function readmeManifestCommandClaims (sampleRoot: string): ReadmeCommandClaim[] {
  const readme = readdirSync(sampleRoot).find((name) => /^readme\.md$/i.test(name))
  if (!readme) {
    return []
  }
  const content = readFileSync(path.join(sampleRoot, readme), 'utf8')
  const claims: ReadmeCommandClaim[] = []
  const patterns = [
    { expression: /`([^`\r\n]+)`\s+as\s+(?:an?\s+)?(slash|mention)\s+command/gi, triggers: undefined },
    { expression: /`([^`\r\n]+)`\s+for\s+both\s+command\s+surfaces/gi, triggers: ['mention', 'slash'] },
  ]
  const manifestClaims = content
    .split(/\r?\n\s*\r?\n/)
    .filter((paragraph) => /manifest/i.test(paragraph) && /declar/i.test(paragraph))
  for (const paragraph of manifestClaims) {
    for (const pattern of patterns) {
      for (const match of paragraph.matchAll(pattern.expression)) {
        if (!match[1] || claims.some((claim) => claim.title === match[1])) {
          continue
        }
        claims.push({ title: match[1], triggers: pattern.triggers ?? [match[2]!.toLowerCase()] })
      }
    }
  }
  return claims
}

function manifestBotCommands (manifest: Record<string, unknown>): Map<string, string[][]> {
  const commands = new Map<string, string[][]>()
  if (!Array.isArray(manifest.bots)) {
    return commands
  }
  for (const bot of manifest.bots) {
    if (!bot || typeof bot !== 'object') {
      continue
    }
    const commandLists = (bot as Record<string, unknown>).commandLists
    if (!Array.isArray(commandLists)) {
      continue
    }
    for (const commandList of commandLists) {
      if (!commandList || typeof commandList !== 'object') {
        continue
      }
      const list = commandList as Record<string, unknown>
      const entries = list.commands
      if (!Array.isArray(entries)) {
        continue
      }
      const triggers = Array.isArray(list.triggers)
        ? list.triggers.filter((trigger): trigger is string => typeof trigger === 'string').sort()
        : ['mention']
      for (const entry of entries) {
        if (!entry || typeof entry !== 'object') {
          continue
        }
        const title = (entry as Record<string, unknown>).title
        if (typeof title === 'string') {
          commands.set(title, [...(commands.get(title) ?? []), triggers])
        }
      }
    }
  }
  return commands
}

function sourceRouteIds (sources: string): string[] {
  return [...sources.matchAll(/\[TeamsQueryRoute\s*\(\s*"([^"]+)"/g)].map((match) => match[1]!)
}

function manifestComposeCommands (manifest: Record<string, unknown>): Map<string, string[]> {
  const commands = new Map<string, string[]>()
  if (!Array.isArray(manifest.composeExtensions)) {
    return commands
  }
  for (const extension of manifest.composeExtensions) {
    if (!extension || typeof extension !== 'object') {
      continue
    }
    const entries = (extension as Record<string, unknown>).commands
    if (!Array.isArray(entries)) {
      continue
    }
    for (const entry of entries) {
      if (!entry || typeof entry !== 'object') {
        continue
      }
      const command = entry as Record<string, unknown>
      if (typeof command.id === 'string' && typeof command.type === 'string') {
        commands.set(command.id, [...(commands.get(command.id) ?? []), command.type])
      }
    }
  }
  return commands
}

function manifestLinkDomains (manifest: Record<string, unknown>): string[] {
  if (!Array.isArray(manifest.composeExtensions)) {
    return []
  }
  return manifest.composeExtensions.flatMap((extension) => {
    if (!extension || typeof extension !== 'object') {
      return []
    }
    const handlers = (extension as Record<string, unknown>).messageHandlers
    if (!Array.isArray(handlers)) {
      return []
    }
    return handlers.flatMap((handler) => {
      if (!handler || typeof handler !== 'object' || (handler as Record<string, unknown>).type !== 'link') {
        return []
      }
      const value = (handler as Record<string, unknown>).value
      const domains = value && typeof value === 'object' ? (value as Record<string, unknown>).domains : undefined
      return Array.isArray(domains) ? domains.filter((domain): domain is string => typeof domain === 'string') : []
    })
  })
}

export async function fetchSchema (url: string): Promise<unknown> {
  const address = new URL(url)
  if (
    address.protocol !== 'https:' ||
    address.hostname !== 'developer.microsoft.com' ||
    !/^\/json-schemas\/teams\/v\d+\.\d+\/MicrosoftTeams\.schema\.json$/.test(address.pathname) ||
    address.search ||
    address.hash
  ) {
    throw new SyncError('Only released Teams manifest schema URLs are supported')
  }
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), 20_000)
  try {
    const response = await fetch(url, {
      headers: { 'User-Agent': 'teams-sample-sync/3' },
      signal: controller.signal,
      redirect: 'error',
    })
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`)
    }
    const bytes = new Uint8Array(await response.arrayBuffer())
    if (bytes.byteLength > 5_000_000) {
      throw new Error('Schema response exceeds 5 MB')
    }
    return JSON.parse(new TextDecoder().decode(bytes))
  } catch (error) {
    throw new SyncError(
      `Released manifest schema is unavailable: ${error instanceof Error ? error.message : String(error)}`
    )
  } finally {
    clearTimeout(timer)
  }
}

function schemaError (error: ErrorObject): string {
  const location = error.instancePath.replace(/^\//, '').replaceAll('/', '.') || '<root>'
  const additionalProperty =
    error.keyword === 'additionalProperties' && typeof error.params.additionalProperty === 'string'
      ? error.params.additionalProperty
      : undefined
  if (additionalProperty) {
    const propertyPath = location === '<root>' ? additionalProperty : `${location}.${additionalProperty}`
    return `Manifest schema error at ${propertyPath}: property is not allowed by the released schema`
  }
  return `Manifest schema error at ${location}: ${error.message ?? error.keyword}`
}

export async function checkManifest (
  sampleRoot: string,
  manifestTarget: ManifestTarget,
  loadSchema = fetchSchema
): Promise<string[]> {
  const errors: string[] = []
  const packageRoot = path.join(sampleRoot, manifestTarget.packageDirectory)
  const manifestPath = path.join(packageRoot, 'manifest.json')
  if (!existsSync(manifestPath)) {
    return [`Missing ${manifestTarget.packageDirectory}/manifest.json`]
  }
  const raw = readFileSync(manifestPath, 'utf8')
  const yamlDocument = parseDocument(raw, { uniqueKeys: true })
  const duplicate = yamlDocument.errors.find((error) => error.code === 'DUPLICATE_KEY')
  if (duplicate) {
    return [`Invalid manifest JSON: ${duplicate.message}`]
  }
  let manifest: Record<string, unknown>
  try {
    const value: unknown = JSON.parse(raw)
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      throw new Error('root must be an object')
    }
    manifest = value as Record<string, unknown>
  } catch (error) {
    return [`Invalid manifest JSON: ${error instanceof Error ? error.message : String(error)}`]
  }
  for (const key of ['$schema', 'manifestVersion', 'version', 'id', 'name', 'description', 'icons']) {
    if (!(key in manifest)) {
      errors.push(`Manifest is missing ${key}`)
    }
  }
  for (const name of ['manifest.json', 'color.png', 'outline.png']) {
    if (existsSync(path.join(sampleRoot, name))) {
      errors.push(`Manifest asset is outside ${manifestTarget.packageDirectory}: ${name}`)
    }
  }
  const icons = manifest.icons && typeof manifest.icons === 'object' ? (manifest.icons as Record<string, unknown>) : {}
  for (const name of ['color', 'outline']) {
    const icon = icons[name]
    if (typeof icon !== 'string' || path.basename(icon) !== icon || !existsSync(path.join(packageRoot, icon))) {
      errors.push(`Manifest icon is missing or outside package root: ${name}`)
    }
  }
  const sources = allFiles(sampleRoot)
    .filter(
      (file) =>
        file.endsWith('.cs') && !['bin', 'obj', 'tests'].includes(path.relative(sampleRoot, file).split(path.sep)[0]!)
    )
    .map((file) => readFileSync(file, 'utf8'))
    .join('\n')
  if (sources.includes('AgentApplication') && (!Array.isArray(manifest.bots) || manifest.bots.length === 0)) {
    errors.push('Manifest bots capability does not match the Agents application source')
  }
  if (
    /Teams(?:Query|SubmitAction|FetchAction|QueryLink|SelectItem)Route/.test(sources) &&
    (!Array.isArray(manifest.composeExtensions) || manifest.composeExtensions.length === 0)
  ) {
    errors.push('Manifest composeExtensions capability does not match message-extension routes')
  }
  const composeCommands = manifestComposeCommands(manifest)
  for (const routeId of sourceRouteIds(sources)) {
    const declarations = composeCommands.get(routeId) ?? []
    if (declarations.length !== 1 || declarations[0] !== 'query') {
      errors.push(`TeamsQueryRoute "${routeId}" requires exactly one composeExtensions command with type "query"`)
    }
  }
  if (/\[TeamsQueryLinkRoute(?:\]|\s)/.test(sources)) {
    const declaredDomains = manifestLinkDomains(manifest).sort()
    if (declaredDomains.length === 0) {
      errors.push('TeamsQueryLinkRoute requires a composeExtensions link message handler with declared domains')
    } else if (declaredDomains.some((domain) => domain.includes('*'))) {
      errors.push('TeamsQueryLinkRoute link-handler domains must be exact domains, not wildcards')
    }
  }
  const declaredCommands = manifestBotCommands(manifest)
  for (const claim of readmeManifestCommandClaims(sampleRoot)) {
    const declarations = declaredCommands.get(claim.title) ?? []
    if (declarations.length === 0) {
      errors.push(
        `README says the app manifest declares bot command "${claim.title}", but bots[].commandLists does not contain it`
      )
    } else {
      const triggers = [...new Set(declarations.flat())].sort()
      if (triggers.join(',') !== claim.triggers.join(',')) {
        errors.push([
          `README requires bot command "${claim.title}" triggers [${claim.triggers.join(', ')}],`,
          `but its commandLists entries have [${triggers.join(', ')}]`,
        ].join(' '))
      }
    }
  }
  const version = manifest.manifestVersion
  const schemaUrl = manifest.$schema
  if (typeof version !== 'string' || typeof schemaUrl !== 'string') {
    return errors
  }
  let url: URL
  try {
    url = new URL(schemaUrl)
  } catch {
    errors.push('Manifest $schema is not a valid URL')
    return errors
  }
  const suffix = `/json-schemas/teams/v${version}/MicrosoftTeams.schema.json`
  if (url.protocol !== 'https:' || url.hostname !== 'developer.microsoft.com' || url.pathname !== suffix) {
    errors.push('Manifest $schema does not match manifestVersion on developer.microsoft.com')
    return errors
  }
  const ajv = new Ajv({ allErrors: true, strict: false, unicodeRegExp: false, validateSchema: false, logger: false })
  const validate = ajv.compile((await loadSchema(schemaUrl)) as AnySchema)
  if (!validate(renderPlaceholders(manifest))) {
    errors.push(...(validate.errors ?? []).slice(0, 10).map(schemaError))
  }
  return errors
}

export function sanitizedChildEnvironment (extra: Record<string, string> = {}): NodeJS.ProcessEnv {
  const retained = [
    'PATH',
    'Path',
    'SystemRoot',
    'WINDIR',
    'HOME',
    'USERPROFILE',
    'TMP',
    'TEMP',
    'DOTNET_ROOT',
    'DOTNET_CLI_HOME',
    'NUGET_PACKAGES',
    'SSL_CERT_FILE',
  ]
  const environment: NodeJS.ProcessEnv = {}
  for (const key of retained) {
    if (process.env[key] !== undefined) {
      environment[key] = process.env[key]
    }
  }
  for (const [key, value] of Object.entries(extra)) {
    environment[key] = value
  }
  return environment
}

const activeProcesses = new Set<ReturnType<typeof spawn>>()
const networkFailure = new RegExp([
  'NU13(?:00|01)',
  'unable to load the service index',
  'name or service not known',
  'temporary failure in name resolution',
  'connection (?:refused|timed out)',
  'TLS handshake',
  'network is unreachable',
].join('|'), 'i')

export function cancelValidationProcesses (): void {
  for (const child of activeProcesses) {
    terminate(child)
  }
}

export async function commandErrors (
  command: string,
  args: string[],
  cwd: string,
  timeoutMs = 10 * 60_000
): Promise<string[]> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd,
      detached: process.platform !== 'win32',
      env: sanitizedChildEnvironment(),
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    activeProcesses.add(child)
    let output = ''
    let timedOut = false
    const timer = setTimeout(() => {
      timedOut = true
      terminate(child)
    }, timeoutMs)
    const append = (chunk: Buffer): void => {
      output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
    }
    child.stdout.on('data', append)
    child.stderr.on('data', append)
    const cleanup = (): void => {
      clearTimeout(timer)
      activeProcesses.delete(child)
    }
    child.on('error', (error) => {
      cleanup()
      reject(new SyncError(`Cannot run ${command}: ${error.message}`))
    })
    child.on('close', (code) => {
      cleanup()
      if (timedOut) {
        reject(new SyncError(`Validation command timed out: ${command} ${args.join(' ')}`))
        return
      }
      if (code === 0) {
        resolve([])
        return
      }
      if (networkFailure.test(output)) {
        reject(new SyncError(`Validation infrastructure failed while running ${command}:\n${output.trim()}`))
        return
      }
      resolve([`${command} ${args.join(' ')} failed:\n${output.trim()}`])
    })
  })
}

function terminate (child: ReturnType<typeof spawn>): void {
  if (!child.pid) {
    return
  }
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true })
    return
  }
  try {
    process.kill(-child.pid, 'SIGKILL')
    return
  } catch {
    /* Child may not own a process group. */
  }
  child.kill('SIGKILL')
}

async function httpSmoke (sampleRoot: string, project: string): Promise<string[]> {
  const port = 41000 + Math.floor(Math.random() * 10000)
  const url = `http://127.0.0.1:${port}`
  const child = spawn('dotnet', ['run', '--project', project, '--no-build', '--no-restore', '--urls', url], {
    cwd: sampleRoot,
    env: sanitizedChildEnvironment({ ASPNETCORE_URLS: url, ASPNETCORE_ENVIRONMENT: 'Development' }),
    stdio: ['ignore', 'pipe', 'pipe'],
    detached: process.platform !== 'win32',
  })
  activeProcesses.add(child)
  let spawnError: Error | undefined
  child.on('error', (error) => {
    spawnError = error
  })
  let output = ''
  child.stdout.on('data', (chunk: Buffer) => {
    output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
  })
  child.stderr.on('data', (chunk: Buffer) => {
    output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
  })
  try {
    for (let count = 0; count < 40; count += 1) {
      if (spawnError) {
        throw new SyncError(`Cannot start HTTP smoke process: ${spawnError.message}`)
      }
      if (child.exitCode !== null) {
        return [`HTTP smoke process exited before readiness:\n${output.trim()}`]
      }
      try {
        const response = await fetch(`${url}/`, { signal: AbortSignal.timeout(500) })
        if (response.status === 200) {
          return []
        }
      } catch {
        /* Wait for startup. */
      }
      await delay(500)
    }
    return [`HTTP smoke GET / did not return 200:\n${output.trim()}`]
  } finally {
    terminate(child)
    activeProcesses.delete(child)
  }
}

export function prepareManifest (sampleRoot: string, canonicalRoot: string, target: ManifestTarget): void {
  const packageRoot = path.join(sampleRoot, target.packageDirectory)
  mkdirSync(packageRoot, { recursive: true })
  for (const name of ['color.png', 'outline.png']) {
    const destination = path.join(packageRoot, name)
    const source = path.join(canonicalRoot, 'appManifest', name)
    if (!existsSync(destination) && existsSync(source)) {
      copyFileSync(source, destination)
    }
  }
}

export interface ValidationRuntime {
  runCommand: (command: string, args: string[], cwd: string) => string[] | Promise<string[]>
  runHttpSmoke: (sampleRoot: string, project: string) => Promise<string[]>
  loadSchema?: typeof fetchSchema
}

export const defaultValidationRuntime: ValidationRuntime = {
  runCommand: commandErrors,
  runHttpSmoke: httpSmoke,
}

export async function validateSample (
  repo: string,
  sample: string,
  sampleRoot: string,
  configured: Targets,
  target: ManifestTarget,
  excludes: string[],
  selectedAgentsSdkVersion: string,
  runtime: ValidationRuntime = defaultValidationRuntime,
  group: 'code' | 'manifest' | 'all' = 'all'
): Promise<ValidationResult> {
  if (!['code', 'manifest', 'all'].includes(group)) {
    throw new SyncError('Invalid validation group')
  }
  const checks: Record<string, ValidationCheck> = {}
  const errors: string[] = []
  const result = (name: string, failures: string[]): void => {
    checks[name] = { status: failures.length ? 'failed' : 'passed', errors: failures }
    errors.push(...failures)
  }
  const skip = (name: string, reason?: string): void => {
    checks[name] = { status: reason ? 'not-run' : 'skipped', errors: reason ? [reason] : [] }
  }
  let project: string | undefined
  let buildPassed = false
  if (group !== 'manifest') {
    const projectCheck = checkProject(sampleRoot, configured, selectedAgentsSdkVersion)
    project = projectCheck.project
    result('project', projectCheck.errors)
    if (project) {
      const projectArgument = path.relative(sampleRoot, project)
      rmSync(path.join(path.dirname(project), 'obj', 'project.assets.json'), { force: true })
      const restore = await runtime.runCommand('dotnet', ['restore', projectArgument, '--nologo'], sampleRoot)
      if (restore.length === 0) {
        restore.push(...checkRestoredPackages(project, selectedAgentsSdkVersion))
      }
      result('restore', restore)
      if (restore.length === 0) {
        const build = await runtime.runCommand(
          'dotnet',
          ['build', projectArgument, '--no-restore', '--nologo', '--warnaserror'],
          sampleRoot
        )
        result('build', build)
        buildPassed = build.length === 0
      } else {
        skip('build', 'Build requires a successful restore')
      }
    } else {
      skip('restore', 'Restore requires a project')
      skip('build', 'Build requires a project')
    }
  } else {
    for (const name of ['project', 'restore', 'build']) {
      skip(name)
    }
  }
  if (group !== 'code') {
    result('manifest', await checkManifest(sampleRoot, target, runtime.loadSchema))
  } else {
    skip('manifest')
  }
  if (group === 'all') {
    if (buildPassed && project) {
      result('httpSmoke', await runtime.runHttpSmoke(sampleRoot, path.relative(sampleRoot, project)))
    } else {
      skip('httpSmoke', 'HTTP smoke requires a successful build')
    }
  } else {
    skip('httpSmoke')
  }
  const hasContracts = [
    'agent-targeted-messages',
    'bot-ai-messages',
    'bot-attachments',
    'bot-cards',
    'bot-meetings',
    'bot-message-extensions',
    'bot-task-modules',
  ].includes(sample)
  if (group !== 'manifest' && hasContracts) {
    if (buildPassed) {
      result(
        'contracts',
        await runtime.runCommand(
          'dotnet',
          [
            'test',
            path.join(repo, 'automation/teams-sample-sync/tests/contracts/TeamsSampleSync.ContractTests.csproj'),
            '--nologo',
            '--warnaserror',
            `-p:AgentsSdkTestVersion=${selectedAgentsSdkVersion}`,
            '--filter',
            'Sample=' + sample,
          ],
          repo
        )
      )
    } else {
      skip('contracts', 'Contract tests require a successful build')
    }
  } else {
    skip('contracts')
  }
  const testsRoot = path.join(sampleRoot, 'tests')
  const testProjects = existsSync(testsRoot) ? readdirSync(testsRoot).filter((name) => name.endsWith('.csproj')) : []
  if (group !== 'manifest' && testProjects.length > 0) {
    if (testProjects.length !== 1) {
      result('sampleTests', ['Expected exactly one tests/*.csproj when sample tests are present'])
    } else if (!buildPassed) {
      skip('sampleTests', 'Sample tests require a successful build')
    } else {
      result(
        'sampleTests',
        await runtime.runCommand(
          'dotnet',
          ['test', path.relative(sampleRoot, path.join(testsRoot, testProjects[0]!)), '--nologo', '--warnaserror'],
          sampleRoot
        )
      )
    }
  } else {
    skip('sampleTests')
  }
  return {
    version: 2,
    id: randomUUID(),
    sample,
    group,
    passed:
      errors.length === 0 &&
      Object.values(checks).every((check) => check.status !== 'failed' && check.status !== 'not-run'),
    repairable: true,
    outputDigest: digestDirectory(sampleRoot, excludes),
    checks,
    errors,
    externalValidationRequired: ['Credentialed Teams, Entra, Graph, Azure Bot, and portal behavior when applicable'],
  }
}

export function assertFullValidation (value: ValidationResult, sample: string, sampleRoot?: string): void {
  if (
    value.version !== 2 ||
    value.sample !== sample ||
    value.group !== 'all' ||
    !value.passed ||
    !value.id ||
    !value.outputDigest ||
    value.errors.length
  ) {
    throw new SyncError('Current full validation is required')
  }
  for (const key of ['project', 'restore', 'build', 'manifest', 'httpSmoke']) {
    if (value.checks[key]?.status !== 'passed') {
      throw new SyncError(`Required validation check did not pass: ${key}`)
    }
  }
  const hasContracts = [
    'agent-targeted-messages',
    'bot-ai-messages',
    'bot-attachments',
    'bot-cards',
    'bot-meetings',
    'bot-message-extensions',
    'bot-task-modules',
  ].includes(sample)
  if (hasContracts && value.checks.contracts?.status !== 'passed') {
    throw new SyncError('Protected contracts did not pass')
  }
  if (
    sampleRoot &&
    existsSync(path.join(sampleRoot, 'tests')) &&
    readdirSync(path.join(sampleRoot, 'tests')).some((name) => name.endsWith('.csproj')) &&
    value.checks.sampleTests?.status !== 'passed'
  ) {
    throw new SyncError('Sample regression tests did not pass')
  }
  if (Object.values(value.checks).some((check) => !['passed', 'skipped'].includes(check.status) || check.errors.length)) {
    throw new SyncError('Validation contains failing or incomplete checks')
  }
}

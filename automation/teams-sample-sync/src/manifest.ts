/** Released Teams manifest schema, assets and capability consistency. */
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync
} from 'node:fs'
import path from 'node:path'
import { Ajv, type AnySchema, type ErrorObject } from 'ajv'
import { parseDocument } from 'yaml'
import { SyncError } from './config.js'
import { dotnetCapabilities } from './dotnet-capabilities.js'
import { capabilityErrors } from './manifest-capabilities.js'
import type { SourceCapabilities } from './language-validation.js'
import type { ManifestTarget } from './types.js'

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
    {
      expression: /`([^`\r\n]+)`\s+as\s+(?:an?\s+)?(slash|mention)\s+command/gi,
      triggers: undefined
    },
    {
      expression: /`([^`\r\n]+)`\s+for\s+both\s+command\s+surfaces/gi,
      triggers: ['mention', 'slash']
    },
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

        claims.push({
          title: match[1],
          triggers: pattern.triggers ?? [match[2]!.toLowerCase()]
        })
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
  loadSchema = fetchSchema,
  inspectCapabilities: (root: string) => SourceCapabilities = dotnetCapabilities
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

  if (raw.includes('$' + '{{BOT_ID}}')) {
    errors.push('Use the repository application client ID placeholder: $' + '{{AAD_APP_CLIENT_ID}}')
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

  errors.push(...capabilityErrors(manifest, inspectCapabilities(sampleRoot), manifestTarget))

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

  const ajv = new Ajv({
    allErrors: true,
    strict: false,
    unicodeRegExp: false,
    validateSchema: false,
    logger: false
  })
  const validate = ajv.compile((await loadSchema(schemaUrl)) as AnySchema)

  if (!validate(renderPlaceholders(manifest))) {
    errors.push(...(validate.errors ?? []).slice(0, 10).map(schemaError))
  }

  return errors
}

export function prepareManifest (
  sampleRoot: string,
  canonicalRoot: string,
  target: ManifestTarget,
  assetRoot = path.join(canonicalRoot, 'appManifest')
): void {
  const packageRoot = path.join(sampleRoot, target.packageDirectory)
  mkdirSync(packageRoot, { recursive: true })

  for (const name of ['color.png', 'outline.png']) {
    const destination = path.join(packageRoot, name)
    const source = path.join(assetRoot, name)

    if (!existsSync(destination) && existsSync(source)) {
      copyFileSync(source, destination)
    }
  }
}

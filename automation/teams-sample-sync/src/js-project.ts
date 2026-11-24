/** JS project, lockfile and installed dependency checks. */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import semver from 'semver'
import { record, relativePath, SyncError } from './config.js'
import { assertJsSdkSelection, JS_SDK_PACKAGES } from './js-versions.js'
import type { AgentsSdkVersionSelection } from './types.js'

export function jsonRecord (file: string): Record<string, unknown> {
  return record(JSON.parse(readFileSync(file, 'utf8')), file)
}

export function checkJsProject (root: string, selection: AgentsSdkVersionSelection): string[] {
  const errors: string[] = []
  assertJsSdkSelection(selection, selection.minimumAgentsSdkVersion)
  let project: Record<string, unknown>
  let config: Record<string, unknown>

  try {
    project = jsonRecord(path.join(root, 'package.json'))
    config = jsonRecord(path.join(root, 'tsconfig.json'))
  } catch (error) {
    return [`Cannot read JS project configuration: ${error instanceof Error ? error.message : String(error)}`]
  }

  if (project.type !== 'module' || project.private !== true) {
    errors.push('JS samples must be private ES module packages')
  }

  const hasDependencyOverrides = project.workspaces || project.overrides
  const hasNpmConfiguration = existsSync(path.join(root, '.npmrc'))
  const hasShrinkwrap = existsSync(path.join(root, 'npm-shrinkwrap.json'))

  if (hasDependencyOverrides || hasNpmConfiguration || hasShrinkwrap) {
    errors.push('Sample workspaces, dependency overrides, project .npmrc and shrinkwrap overrides are not supported')
  }

  const dependencies = record(project.dependencies, 'package dependencies')

  for (const name of JS_SDK_PACKAGES) {
    if (dependencies[name] !== selection.packages![name]) {
      errors.push(`${name} must declare the frozen exact version ${selection.packages![name]}`)
    }
  }

  for (const group of ['dependencies', 'devDependencies', 'optionalDependencies']) {
    const values = project[group] === undefined ? {} : record(project[group], group)

    for (const [name, version] of Object.entries(values)) {
      if (typeof version !== 'string' || !semver.validRange(version)) {
        errors.push(`${name} must use a registry semver dependency`)
      }

      if (/^(?:botbuilder|botframework-|@microsoft\/teams\.(?:apps|plugins)|@microsoft\/teamsfx)/.test(name)) {
        errors.push(`Legacy Teams or Bot Framework runtime dependency remains: ${name}`)
      }
    }
  }

  const scripts = project.scripts === undefined ? {} : record(project.scripts, 'package scripts')

  if (Object.keys(scripts).some((name) => /^(?:pre|post)(?:install|build|start|test)$|^prepare$|^install$/.test(name))) {
    errors.push('Implicit lifecycle scripts are not supported; keep build and startup commands explicit')
  }

  const compiler = record(config.compilerOptions, 'TypeScript compiler options')
  const supportedOptions = new Set([
    'target', 'module', 'moduleResolution', 'rootDir', 'outDir', 'strict',
    'skipLibCheck', 'forceConsistentCasingInFileNames', 'noUncheckedIndexedAccess',
    'esModuleInterop', 'resolveJsonModule', 'exactOptionalPropertyTypes',
  ])
  const hasExternalConfiguration = config.extends || config.references
  const hasUnsupportedOptions = Object.keys(compiler).some((key) => !supportedOptions.has(key))
  const hasExpectedOutput = compiler.outDir === 'dist' && compiler.rootDir === 'src' && compiler.noEmit !== true
  const hasExpectedModuleMode = compiler.module === 'NodeNext' && compiler.moduleResolution === 'NodeNext'
  const hasExpectedSources = JSON.stringify(config.include) === JSON.stringify(['src/**/*.ts'])

  if (hasExternalConfiguration || hasUnsupportedOptions || !hasExpectedOutput || !hasExpectedModuleMode ||
    compiler.strict !== true || !hasExpectedSources) {
    errors.push('TypeScript must compile src/**/*.ts in strict NodeNext mode from src to dist without external configuration')
  }

  if (!existsSync(path.join(root, 'src/index.ts')) || !existsSync(path.join(root, 'src/agent.ts'))) {
    errors.push('JS sample requires src/index.ts startup and an importable src/agent.ts factory')
  }

  return errors
}

export function checkJsPackages (root: string, selection: AgentsSdkVersionSelection, installed: boolean): string[] {
  let lock: Record<string, unknown>

  try {
    lock = jsonRecord(path.join(root, 'package-lock.json'))
  } catch (error) {
    return [`Cannot read npm lockfile: ${error instanceof Error ? error.message : String(error)}`]
  }

  if (lock.lockfileVersion !== 3) {
    return ['npm lockfile must use version 3']
  }

  const packages = record(lock.packages, 'lockfile packages')
  const declared = record(record(packages[''], 'lockfile root').dependencies, 'lockfile root dependencies')
  const errors: string[] = []

  for (const name of JS_SDK_PACKAGES) {
    const expected = selection.packages![name]
    const metadata = record(packages[`node_modules/${name}`], `locked ${name}`)

    if (declared[name] !== expected || metadata.version !== expected || typeof metadata.integrity !== 'string') {
      errors.push(`${name} lockfile version or integrity differs from the frozen selection`)
    }

    if (installed) {
      try {
        if (jsonRecord(path.join(root, 'node_modules', name, 'package.json')).version !== expected) {
          errors.push(`${name} installed version differs from the frozen selection`)
        }
      } catch {
        errors.push(`${name} installed package is unavailable`)
      }
    }
  }

  for (const [location, raw] of Object.entries(packages)) {
    if (!location) {
      continue
    }

    const metadata = record(raw, `locked ${location}`)

    if (
      !relativePath(location) || !location.startsWith('node_modules/') ||
      metadata.link || typeof metadata.resolved !== 'string' ||
      !metadata.resolved.startsWith('https://registry.npmjs.org/') || typeof metadata.integrity !== 'string'
    ) {
      errors.push(`Dependency must be integrity-pinned to the public npm registry: ${location}`)
    }

    for (const name of JS_SDK_PACKAGES) {
      if (location.endsWith(`node_modules/${name}`) && metadata.version !== selection.packages![name]) {
        errors.push(`Conflicting nested Agents SDK version: ${location}`)
      }
    }
  }

  return errors
}

export function requireJsSelection (selection?: AgentsSdkVersionSelection): AgentsSdkVersionSelection {
  if (!selection) {
    throw new SyncError('JS validation requires the frozen npm package selection')
  }

  assertJsSdkSelection(selection, selection.minimumAgentsSdkVersion)

  return selection
}

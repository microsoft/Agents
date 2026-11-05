/** Trusted npm preparation, compilation and protected JS behavior checks. */
import {
  existsSync,
  lstatSync,
  mkdirSync,
  rmSync,
  writeFileSync
} from 'node:fs'
import path from 'node:path'
import { jsCapabilities } from './js-capabilities.js'
import { checkJsPackages, checkJsProject, requireJsSelection } from './js-project.js'
import { SyncError } from './config.js'
import type { CodeValidationInput, LanguageValidation } from './language-validation.js'
import type { ValidationCheck } from './types.js'

const REQUIRED_CHECKS = ['project', 'restore', 'packages', 'build', 'manifest', 'contracts', 'httpSmoke']

function inspectProjectEvidence (check: () => string[]): string[] {
  try {
    return check()
  } catch (error) {
    return [`Invalid JS project evidence: ${error instanceof Error ? error.message : String(error)}`]
  }
}

async function validateCode (input: CodeValidationInput): Promise<Record<string, ValidationCheck>> {
  const {
    repo, sample, sampleRoot, configured, runtime, group
  } = input
  const checks: Record<string, ValidationCheck> = {}
  const recordCheck = (name: string, errors: string[]): boolean => {
    checks[name] = {
      status: errors.length ? 'failed' : 'passed',
      errors
    }

    return errors.length === 0
  }

  const skipCheck = (name: string, reason?: string): void => {
    checks[name] = {
      status: reason ? 'not-run' : 'skipped',
      errors: reason ? [reason] : []
    }
  }

  if (group === 'manifest') {
    for (const name of REQUIRED_CHECKS.filter((name) => name !== 'manifest')) {
      skipCheck(name)
    }

    return checks
  }

  const selection = requireJsSelection(input.selection)
  const project = inspectProjectEvidence(() => checkJsProject(sampleRoot, selection))
  project.push(...(jsCapabilities(sampleRoot).errors ?? []))

  if (!recordCheck('project', project)) {
    for (const name of ['restore', 'packages', 'build', 'contracts', 'httpSmoke']) {
      skipCheck(name, 'Requires a valid JS project')
    }

    return checks
  }

  const npmConfigRoot = path.join(repo, '.sync', 'npm-config')
  mkdirSync(npmConfigRoot, { recursive: true })
  const userConfig = path.join(npmConfigRoot, 'user.npmrc')
  const globalConfig = path.join(npmConfigRoot, 'global.npmrc')
  writeFileSync(userConfig, '', 'utf8')
  writeFileSync(globalConfig, '', 'utf8')
  const npmOptions = [
    '--ignore-scripts', '--no-audit', '--no-fund', '--registry=https://registry.npmjs.org',
    `--userconfig=${userConfig}`, `--globalconfig=${globalConfig}`,
  ]
  // The agent cannot create a trustworthy lockfile through text edits. npm owns
  // preparation, including initial imports and changed dependency declarations.
  const prepared = await runtime.runCommand('npm', ['install', '--package-lock-only', ...npmOptions], sampleRoot)

  if (!recordCheck('restore', prepared)) {
    for (const name of ['packages', 'build', 'contracts', 'httpSmoke']) {
      skipCheck(name, 'Requires successful npm lockfile preparation')
    }

    return checks
  }

  const packages = inspectProjectEvidence(() => checkJsPackages(sampleRoot, selection, false))

  if (packages.length === 0) {
    packages.push(...await runtime.runCommand('npm', ['ci', ...npmOptions], sampleRoot))

    if (packages.length === 0) {
      packages.push(...inspectProjectEvidence(() => checkJsPackages(sampleRoot, selection, true)))
    }
  }

  if (!recordCheck('packages', packages)) {
    for (const name of ['build', 'contracts', 'httpSmoke']) {
      skipCheck(name, 'Requires verified installed packages')
    }

    return checks
  }

  const output = path.join(sampleRoot, 'dist')

  if (existsSync(output) && lstatSync(output).isSymbolicLink()) {
    throw new SyncError('JS generated output must not be a symlink')
  }

  // Contracts must execute fresh output from the source covered by the digest.
  // Ignored build products are writable by the agent and cannot be trusted.
  rmSync(output, {
    recursive: true,
    force: true
  })
  rmSync(path.join(sampleRoot, 'tsconfig.tsbuildinfo'), { force: true })
  const compiler = path.join(sampleRoot, 'node_modules/typescript/bin/tsc')
  const build = await runtime.runCommand(process.execPath, [compiler, '--build', 'tsconfig.json', '--force'], sampleRoot)

  if (!recordCheck('build', build)) {
    skipCheck('contracts', 'Requires a successful build')
    skipCheck('httpSmoke', 'Requires a successful build')

    return checks
  }

  const contract = path.join(repo, configured.contractDirectory, `${sample}.test.mjs`)
  recordCheck('contracts', existsSync(contract)
    ? await runtime.runCommand(process.execPath, [contract, sampleRoot], repo)
    : [`Protected JS contracts are missing for ${sample}`])

  if (group === 'all') {
    recordCheck('httpSmoke', await runtime.runHttpSmoke(sampleRoot, 'dist/index.js', 'js'))
  } else {
    skipCheck('httpSmoke')
  }

  return checks
}

export const jsValidation: LanguageValidation = {
  validateCode,
  capabilities: jsCapabilities,
  requiredChecks: () => [...REQUIRED_CHECKS],
}

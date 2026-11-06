import assert from 'node:assert/strict'
import {
  existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { targets } from '../../src/config.js'
import { checkJsPackages, checkJsProject } from '../../src/js-project.js'
import { JS_SDK_PACKAGES } from '../../src/js-versions.js'
import { assertFullValidation, validateSample, type ValidationRuntime } from '../../src/validate.js'
import type { AgentsSdkVersionSelection } from '../../src/types.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const selection: AgentsSdkVersionSelection = {
  minimumAgentsSdkVersion: '1.9.1',
  selectedAgentsSdkVersion: '1.9.1',
  releaseTag: 'npm:1.9.1',
  packages: Object.fromEntries(JS_SDK_PACKAGES.map((name) => [name, '1.9.1'])),
}

function write (root: string, file: string, value: unknown): void {
  const target = path.join(root, file)
  mkdirSync(path.dirname(target), { recursive: true })
  writeFileSync(target, typeof value === 'string' ? value : JSON.stringify(value))
}

function fixture (): {
  root: string
  sample: string
} {
  const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-js-validation-'))
  const sample = path.join(root, 'sample')
  const baseline = path.join(repo, 'samples/nodejs/teams/bot-cards')
  for (const file of ['package.json', 'tsconfig.json', 'src/agent.ts', 'src/index.ts', 'manifest/manifest.json']) {
    write(sample, file, readFileSync(path.join(baseline, file), 'utf8'))
  }
  write(sample, 'manifest/color.png', 'icon')
  write(sample, 'manifest/outline.png', 'icon')

  return {
    root,
    sample
  }
}

function lockedPackages (): Record<string, unknown> {
  return {
    '': { dependencies: selection.packages },
    ...Object.fromEntries(JS_SDK_PACKAGES.map((name) => [`node_modules/${name}`, {
      version: '1.9.1',
      integrity: 'sha512-test',
      resolved: `https://registry.npmjs.org/${name}/package.tgz`,
    }])),
  }
}

describe('JS validation gates', () => {
  it('prepares a lockfile, installs without scripts, builds and requires protected contracts', async () => {
    const { root, sample } = fixture()
    try {
      const configured = {
        ...targets(repo),
        language: 'js' as const,
        contractDirectory: 'contracts',
        packagePolicy: {
          nodeVersion: '24',
          minimumAgentsSdkVersion: '1.9.1'
        },
      }
      const calls: string[][] = []
      const runtime: ValidationRuntime = {
        runCommand: async (command, args, cwd) => {
          calls.push([command, ...args])
          if (command === 'npm') {
            assert.equal(cwd, sample)
            assert.ok(args.includes('--ignore-scripts'))
            assert.ok(args.some((argument) => argument.startsWith('--userconfig=')))
            if (args[0] === 'install') {
              write(sample, 'package-lock.json', {
                lockfileVersion: 3,
                packages: lockedPackages()
              })
            }
            if (args[0] === 'ci') {
              for (const name of JS_SDK_PACKAGES) {
                write(sample, `node_modules/${name}/package.json`, { version: '1.9.1' })
              }
            }
          }
          if (args.includes('--build')) {
            assert.ok(args.includes('--force'))
            assert.equal(existsSync(path.join(sample, 'dist/agent.js')), false)
            assert.equal(existsSync(path.join(sample, 'tsconfig.tsbuildinfo')), false)
          }

          return []
        },
        runHttpSmoke: async (_root, entry, language) => {
          assert.equal(entry, 'dist/index.js')
          assert.equal(language, 'js')

          return []
        },
        loadSchema: async () => ({ type: 'object' }),
      }
      const validate = () => validateSample(
        root, 'bot-cards', sample, configured, configured.samples['bot-cards']!.manifest,
        ['node_modules/**', 'dist/**'], '1.9.1', runtime, 'all', undefined, selection
      )
      write(sample, 'dist/agent.js', 'untrusted generated output')
      write(sample, 'tsconfig.tsbuildinfo', 'stale build cache')
      const missing = await validate()
      assert.equal(missing.passed, false)
      assert.match(missing.errors.join('\n'), /Protected JS contracts are missing/)
      write(root, 'contracts/bot-cards.test.mjs', '// protected contract fixture')
      const passed = await validate()
      assert.equal(passed.passed, true)
      assert.doesNotThrow(() => assertFullValidation(passed, 'bot-cards', undefined, 'js'))
      assert.ok(calls.some((args) => args.includes('--package-lock-only')))
      assert.ok(calls.some((args) => args.includes('--build')))
      assert.ok(calls.some((args) => args.includes(path.join(root, 'contracts/bot-cards.test.mjs'))))
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects legacy bootstraps, recursive install hooks and conflicting locked SDK versions', () => {
    const { root, sample } = fixture()
    try {
      const project = JSON.parse(readFileSync(path.join(sample, 'package.json'), 'utf8'))
      project.dependencies['@microsoft/teams.apps'] = '2.0.5'
      project.scripts.prebuild = 'npm ci'
      write(sample, 'package.json', project)
      const errors = checkJsProject(sample, selection)
      assert.match(errors.join('\n'), /Legacy Teams/)
      assert.match(errors.join('\n'), /Implicit lifecycle scripts/)
      const configFile = path.join(sample, 'tsconfig.json')
      const config = JSON.parse(readFileSync(configFile, 'utf8'))
      config.compilerOptions.declarationDir = '../../outside'
      write(sample, 'tsconfig.json', config)
      assert.match(checkJsProject(sample, selection).join('\n'), /without external configuration/)
      const packages = lockedPackages()
      packages['node_modules/dependency/node_modules/@microsoft/agents-hosting'] = {
        version: '1.8.0',
        resolved: 'https://registry.npmjs.org/old/package.tgz',
        integrity: 'sha512-test',
      }
      write(sample, 'package-lock.json', {
        lockfileVersion: 3,
        packages
      })
      assert.match(checkJsPackages(sample, selection, false).join('\n'), /Conflicting nested Agents SDK/)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})

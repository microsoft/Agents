/** Offline candidate fixture: fake Copilot transport and npm, real coordinator and evidence gates. */
import { execFileSync } from 'node:child_process'
import {
  mkdirSync, mkdtempSync, readFileSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse, stringify } from 'yaml'
import type { SdkClient, SdkFactory, SdkSession } from '../../src/agent-runner.js'
import { JS_SDK_PACKAGES } from '../../src/js-versions.js'
import type { Configuration } from '../../src/types.js'
import type { ValidationRuntime } from '../../src/validate.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
export const selected = {
  language: 'js',
  sample: 'bot-cards'
} as const
export const versions = {
  js: {
    minimumAgentsSdkVersion: '1.9.1',
    selectedAgentsSdkVersion: '1.9.1',
    releaseTag: 'npm:1.9.1',
    packages: Object.fromEntries(JS_SDK_PACKAGES.map((name) => [name, '1.9.1'])),
  }
}

export function write (root: string, file: string, content: unknown): void {
  const target = path.join(root, file)
  mkdirSync(path.dirname(target), { recursive: true })
  writeFileSync(target, typeof content === 'string' ? content : JSON.stringify(content, null, 2))
}

export function git (root: string, ...args: string[]): string {
  return execFileSync('git', args, {
    cwd: root,
    encoding: 'utf8',
    stdio: 'pipe'
  }).trim()
}

export function commit (root: string): void {
  git(root, 'add', '.')
  git(root, '-c', 'user.name=Sync Tests', '-c', 'user.email=tests@example.com', 'commit', '-qm', 'Fixture snapshot')
}

function initialize (root: string): void {
  git(root, 'init', '-q')
  git(root, 'config', 'core.autocrlf', 'false')
  commit(root)
}

export function candidateFixture () {
  const temp = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-candidate-'))
  const root = path.join(temp, 'repo')
  const upstream = path.join(temp, 'upstream')
  const configured = parse(readFileSync(path.join(repo, 'automation/teams-sample-sync/config/targets.yml'), 'utf8')) as Configuration
  const profile = configured.profiles.js!
  configured.profiles = { js: profile }
  profile.samples = { 'bot-cards': profile.samples['bot-cards']! }
  profile.canonicalSample = 'canonical'
  profile.manifestAssets = 'assets'
  profile.migrationSkill = 'migration-skill'
  profile.syncSkill = 'sync-skill'
  profile.contractDirectory = 'contracts'
  configured.manifestSkill = 'manifest-skill'
  write(root, '.gitignore', '.sync/\nnode_modules/\ndist/\n*.tsbuildinfo\n')
  write(root, 'automation/teams-sample-sync/config/targets.yml', stringify(configured))
  write(root, 'automation/teams-sample-sync/config/ownership.yml', readFileSync(path.join(repo, 'automation/teams-sample-sync/config/ownership.yml'), 'utf8'))
  write(root, 'automation/teams-sample-sync/prompts/agent-prompt.md', 'Migrate the selected sample.')
  write(root, 'automation/teams-sample-sync/package.json', '{}')
  write(root, 'automation/teams-sample-sync/package-lock.json', '{}')
  for (const skill of ['migration-skill', 'sync-skill', 'manifest-skill']) {
    write(root, `${skill}/SKILL.md`, 'Fixture guidance')
  }
  write(root, 'canonical/index.ts', '// canonical guidance')
  for (const name of ['color.png', 'outline.png']) {
    write(root, `assets/${name}`, 'icon')
  }
  write(root, 'contracts/bot-cards.test.mjs', '// protected offline contract')
  write(upstream, 'samples/TeamsSDK/bot-cards/nodejs/bot-cards/index.ts', '// upstream cards behavior')
  initialize(root)
  initialize(upstream)
  const sampleRoot = path.join(root, 'samples/nodejs/teams/bot-cards')
  const output = path.join(root, '.sync/results')
  const commands: string[][] = []
  const lock = {
    lockfileVersion: 3,
    packages: {
      '': { dependencies: versions.js.packages },
      ...Object.fromEntries(JS_SDK_PACKAGES.map((name) => [`node_modules/${name}`, {
        version: '1.9.1',
        integrity: 'sha512-fixture',
        resolved: `https://registry.npmjs.org/${name}/package.tgz`,
      }])),
    }
  }
  const runtime: ValidationRuntime = {
    runCommand: async (command, args) => {
      commands.push([command, ...args])
      if (command === 'npm' && args[0] === 'install') {
        write(sampleRoot, 'package-lock.json', lock)
      }
      if (command === 'npm' && args[0] === 'ci') {
        for (const name of JS_SDK_PACKAGES) {
          write(sampleRoot, `node_modules/${name}/package.json`, { version: '1.9.1' })
        }
      }

      return []
    },
    runHttpSmoke: async () => [],
    loadSchema: async () => ({ type: 'object' }),
  }
  function sdkFactory (implement: () => void, changed: boolean): SdkFactory {
    let turns = 0
    const session: SdkSession = {
      on: (() => () => {}) as SdkSession['on'],
      sendAndWait: async () => {
        turns++
        if (turns === 1) {
          return { data: { content: '## Migration plan\nPreserve cards and package policy.' } } as Awaited<ReturnType<SdkSession['sendAndWait']>>
        }
        implement()

        return { data: { content: `## Self-audit\nOutcome: ${changed ? 'changed' : 'no changes required'}\nSummary: Preserved upstream cards behavior.\nValidated the frozen plan.` } } as Awaited<ReturnType<SdkSession['sendAndWait']>>
      },
      abort: async () => {},
      disconnect: async () => {},
    }
    const client: SdkClient = {
      start: async () => {},
      stop: async () => [],
      getStatus: async () => ({
        version: '1.0.83',
        protocolVersion: 1
      }),
      getAuthStatus: async () => ({ isAuthenticated: true }),
      createSession: async () => session,
    }

    return async () => client
  }
  function importSample (): void {
    const baseline = path.join(repo, 'samples/nodejs/teams/bot-cards')
    for (const file of ['package.json', 'tsconfig.json', 'src/agent.ts', 'src/index.ts', 'manifest/manifest.json', 'README.md']) {
      write(sampleRoot, file, readFileSync(path.join(baseline, file), 'utf8'))
    }
  }

  return {
    temp,
    root,
    upstream,
    sampleRoot,
    output,
    commands,
    runtime,
    sdkFactory,
    importSample
  }
}

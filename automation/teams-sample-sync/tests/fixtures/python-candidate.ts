// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Fake external transports with real Python policy, filesystem and coordinator gates. */
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse, stringify } from 'yaml'
import { candidateFixture as jsFixture, commit, write } from './js-candidate.js'
import { PYTHON_SDK_PACKAGES, selectPythonSdkVersions } from '../../src/python-versions.js'
import { pythonPolicyRuntime } from '../../src/python-runtime.js'
import type { Configuration } from '../../src/types.js'
import type { ValidationRuntime } from '../../src/validate.js'
import { commandErrors } from '../../src/validation-process.js'

export { commit, git, write } from './js-candidate.js'
export const selected = {
  language: 'python',
  sample: 'bot-cards'
} as const
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

export function candidateFixture () {
  const fixture = jsFixture()
  const { root, upstream } = fixture
  const configFile = 'automation/teams-sample-sync/config/targets.yml'
  const configuration = parse(readFileSync(path.join(root, configFile), 'utf8')) as Configuration
  const profile = configuration.profiles.js!
  profile.destinationRoot = 'samples/python/teams'
  profile.packagePolicy = {
    pythonVersion: '3.11.14',
    minimumAgentsSdkVersion: '1.9.0'
  }
  profile.outputDigestExcludes = ['**/__pycache__/**', '**/*.pyc', '.venv/**']
  profile.samples['bot-cards']!.source = 'bot-cards/python/bot-cards'
  configuration.profiles = { python: profile }
  write(root, configFile, stringify(configuration))
  write(root, 'canonical/agent.py', '# Python canonical fixture')
  write(root, 'contracts/bot-cards.py', '# Protected offline fixture suite')
  write(upstream, 'samples/TeamsSDK/bot-cards/python/bot-cards/main.py', '# Pinned upstream fixture')
  commit(root)
  commit(upstream)
  const selection = selectPythonSdkVersions(pythonPolicyRuntime(), '1.9.0', Object.fromEntries(
    PYTHON_SDK_PACKAGES.map((name) => [name, [{
      name,
      version: '1.9.0',
      requires_python: '>=3.11',
      requires_dist: [],
      yanked: false,
    }]])
  ))
  const sampleRoot = path.join(root, 'samples/python/teams/bot-cards')
  const commands: string[][] = []
  const runtime: ValidationRuntime = {
    ...fixture.runtime,
    runCommand: async (command, args) => {
      commands.push([command, ...args])

      if (args[2]?.endsWith('python_environment_digest.py')) {
        return commandErrors(command, args, root)
      }

      if (args[1]?.endsWith('python_prepare.py')) {
        const request = JSON.parse(readFileSync(args[2]!, 'utf8'))
        write(request.environmentRoot, 'offline-installation.txt', 'Fake package transport evidence')
        const interpreter = path.join(request.environmentRoot, process.platform === 'win32' ? 'Scripts/python.exe' : 'bin/python')
        write(path.dirname(args[3]!), path.basename(args[3]!), {
          interpreter: process.platform === 'win32' ? path.toNamespacedPath(interpreter) : interpreter,
          lock: PYTHON_SDK_PACKAGES.map((name) => `${name}==1.9.0 --hash=sha256:${'a'.repeat(64)}`).join('\n') + '\n',
        })
      }

      return []
    },
  }
  const importSample = (): void => {
    write(sampleRoot, 'requirements.txt', PYTHON_SDK_PACKAGES.map((name) => `${name}==1.9.0`).join('\n'))
    write(sampleRoot, 'src/agent.py', 'from microsoft_agents.hosting.core import AgentApplication\napp = AgentApplication()\n')
    write(sampleRoot, 'src/main.py', '# Offline startup fixture')
    write(sampleRoot, 'README.md', 'Offline Python candidate fixture')
    write(sampleRoot, 'manifest/manifest.json', readFileSync(path.join(repo, 'samples/nodejs/teams/bot-cards/manifest/manifest.json'), 'utf8'))
  }

  return {
    ...fixture,
    sampleRoot,
    commands,
    runtime,
    importSample,
    versions: { python: selection },
  }
}

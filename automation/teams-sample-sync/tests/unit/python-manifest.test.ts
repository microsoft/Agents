// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { checkManifest } from '../../src/manifest.js'
import { pythonCapabilities } from '../../src/python-capabilities.js'
import { pythonPolicyRuntime } from '../../src/python-runtime.js'
import { targets } from '../../src/config.js'

const repo = fileURLToPath(new URL('../../../../', import.meta.url))

describe('python-manifest', () => {
  it('Python AST capabilities enforce manifest command IDs, file support and meeting grants', async () => {
    const root = mkdtempSync(path.join(tmpdir(), 'python-manifest-contract-'))

    try {
      mkdirSync(path.join(root, 'src'))
      mkdirSync(path.join(root, 'manifest'))
      writeFileSync(path.join(root, 'requirements.txt'), 'microsoft-agents-activity==1.9.0')
      writeFileSync(path.join(root, 'src/main.py'), '# Not executed')
      writeFileSync(path.join(root, 'src/agent.py'), [
        'from microsoft_agents.hosting.core import AgentApplication as App',
        'from microsoft_agents.hosting.msteams import TeamsAgentExtension as Teams',
        'app = App()',
        'teams = Teams(app)',
        "teams.message_extensions.query('wikipediaSearch')",
        'teams.message_extensions.query_link()',
        'teams.file_consent.accept()',
        'teams.meetings.participants_join()',
      ].join('\n'))

      for (const icon of ['color.png', 'outline.png']) {
        writeFileSync(path.join(root, 'manifest', icon), 'fixture icon')
      }

      const manifest = JSON.parse(readFileSync(path.join(repo, 'samples/nodejs/teams/bot-message-extensions/manifest/manifest.json'), 'utf8'))
      manifest.composeExtensions[0].commands[0].id = 'wrong-command'
      const file = path.join(root, 'manifest/manifest.json')
      writeFileSync(file, JSON.stringify(manifest))
      const configured = targets(repo, 'python').samples['bot-message-extensions']!.manifest
      const inspect = (sampleRoot: string) => pythonCapabilities(pythonPolicyRuntime(), sampleRoot)
      const errors = await checkManifest(root, configured, async () => ({ type: 'object' }), inspect)
      assert.match(errors.join('\n'), /composeExtensions command with type "query"/)
      assert.match(errors.join('\n'), /supportsFiles/)
      assert.match(errors.join('\n'), /OnlineMeetingParticipant.Read.Chat/)
      manifest.composeExtensions[0].commands[0].id = 'wikipediaSearch'
      manifest.bots[0].supportsFiles = true
      manifest.webApplicationInfo = {
        id: '{{MicrosoftAppId}}',
        resource: 'api://{{BOT_DOMAIN}}/{{MicrosoftAppId}}'
      }
      manifest.authorization = {
        permissions: {
          resourceSpecific: [{
            name: 'OnlineMeetingParticipant.Read.Chat',
            type: 'Application'
          }]
        },
      }
      writeFileSync(file, JSON.stringify(manifest))
      assert.deepEqual(await checkManifest(root, configured, async () => ({ type: 'object' }), inspect), [])
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})

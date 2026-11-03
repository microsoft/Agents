import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { targets } from '../../src/config.js'
import { checkManifest } from '../../src/manifest.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

describe('link-domains', () => {
  it('.NET manifests reject unrelated, missing, extra and wildcard link domains against trusted policy', async () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-link-domains-'))
    try {
      const manifest = JSON.parse(readFileSync(path.join(repo, 'samples/dotnet/teams/bot-message-extensions/manifest/manifest.json'), 'utf8'))
      mkdirSync(path.join(root, 'manifest'))
      for (const icon of ['color.png', 'outline.png']) {
        writeFileSync(path.join(root, 'manifest', icon), 'icon')
      }
      for (const language of ['dotnet'] as const) {
        const policy = targets(repo, language).samples['bot-message-extensions']!.manifest
        assert.deepEqual(policy.expectedLinkDomains, ['en.wikipedia.org'])
        for (const domains of [[], ['unrelated.example.org'], ['*.wikipedia.org'], ['en.wikipedia.org', 'other.example.org'], ['en.wikipedia.org']]) {
          manifest.composeExtensions[0].messageHandlers = [{
            type: 'link',
            value: { domains }
          }]
          writeFileSync(path.join(root, 'manifest/manifest.json'), JSON.stringify(manifest))
          const errors = await checkManifest(root, policy, async () => ({ type: 'object' }), () => ({
            bot: true,
            composeExtensions: true,
            queryCommands: [],
            linkHandlerEvidence: 'Link handler',
          }))
          assert.equal(errors.length === 0, domains.join() === 'en.wikipedia.org', `${language}: ${JSON.stringify(domains)}: ${errors.join('; ')}`)
        }
        const { expectedLinkDomains: _expected, ...missingPolicy } = policy
        const errors = await checkManifest(root, missingPolicy, async () => ({ type: 'object' }), () => ({
          bot: true,
          composeExtensions: true,
          queryCommands: [],
          linkHandlerEvidence: 'Link handler',
        }))
        assert.match(errors.join('\n'), /requires trusted expectedLinkDomains/)
      }
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})

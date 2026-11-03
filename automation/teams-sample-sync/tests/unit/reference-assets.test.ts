import assert from 'node:assert/strict'
import {
  mkdtempSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { targets } from '../../src/config.js'
import { prepareManifest } from '../../src/validate.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

describe('maintained reference assets', () => {
  for (const language of ['dotnet'] as const) {
    it(`supplies ${language} initial-import icons without overwriting destination assets`, () => {
      const root = mkdtempSync(path.join(os.tmpdir(), 'teams-reference-icons-'))
      try {
        const configured = targets(repo)
        const target = configured.samples['bot-cards']!.manifest
        const canonical = path.join(repo, configured.canonicalSample)
        const assets = path.join(canonical, 'appManifest')
        prepareManifest(root, canonical, target)
        for (const name of ['color.png', 'outline.png']) {
          assert.deepEqual(readFileSync(path.join(root, target.packageDirectory, name)), readFileSync(path.join(assets, name)))
        }
        const color = path.join(root, target.packageDirectory, 'color.png')
        writeFileSync(color, 'custom sample icon')
        prepareManifest(root, canonical, target)
        assert.equal(readFileSync(color, 'utf8'), 'custom sample icon')
      } finally {
        rmSync(root, {
          recursive: true,
          force: true
        })
      }
    })
  }
})

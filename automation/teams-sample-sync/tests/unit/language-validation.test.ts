import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { checkManifest } from '../../src/manifest.js'
import { assertFullValidation } from '../../src/validate.js'
import type { ValidationResult } from '../../src/types.js'

describe('language validation seam', () => {
  it('checks adapter-provided capabilities without requiring C# source', async () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-capabilities-'))
    try {
      const packageRoot = path.join(root, 'manifest')
      mkdirSync(packageRoot)
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'manifest.json'), JSON.stringify({
        $schema: 'https://developer.microsoft.com/json-schemas/teams/v1.22/MicrosoftTeams.schema.json',
        manifestVersion: '1.22',
        version: '1.0.0',
        id: '00000000-0000-4000-8000-000000000000',
        name: { short: 'Search' },
        description: {
          short: 'Search',
          full: 'Search'
        },
        icons: {
          color: 'color.png',
          outline: 'outline.png'
        },
      }))
      const errors = await checkManifest(
        root,
        {
          distribution: 'sample',
          packageDirectory: 'manifest',
          placeholderConvention: 'double-braces'
        },
        async () => ({ type: 'object' }),
        () => ({
          bot: true,
          composeExtensions: true,
          files: true,
          resourceSpecificPermissions: ['OnlineMeeting.ReadBasic.Chat'],
          queryCommands: [{
            id: 'search',
            evidence: 'Search handler'
          }],
          linkHandlerEvidence: 'Link handler',
        })
      )
      assert.deepEqual(errors, [
        'Meeting event routes require webApplicationInfo with the bot ID and resource',
        'Meeting event routes require Application resource-specific permission OnlineMeeting.ReadBasic.Chat',
        'Manifest bots capability does not match the Agents application source',
        'File consent routes require a bot declaration with supportsFiles enabled',
        'Manifest composeExtensions capability does not match message-extension routes',
        'Search handler requires exactly one composeExtensions command with type "query"',
        'Link handler requires a composeExtensions link message handler with declared domains',
      ])
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects a claimed successful .NET result when protected contracts were skipped', () => {
    const result: ValidationResult = {
      version: 3,
      language: 'dotnet',
      id: 'test-validation',
      sample: 'bot-cards',
      group: 'all',
      passed: true,
      repairable: true,
      outputDigest: 'sha256:test',
      checks: Object.fromEntries(
        ['project', 'restore', 'build', 'manifest', 'httpSmoke'].map((name) => [name, {
          status: 'passed',
          errors: []
        }])
      ),
      errors: [],
      externalValidationRequired: [],
    }
    result.checks.contracts = {
      status: 'skipped',
      errors: []
    }
    assert.throws(() => assertFullValidation(result, 'bot-cards'), /Required validation check did not pass: contracts/)
    result.checks.contracts = {
      status: 'passed',
      errors: []
    }
    assert.doesNotThrow(() => assertFullValidation(result, 'bot-cards'))
  })
})

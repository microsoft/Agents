import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { checkManifest } from '../../src/manifest.js'

const teamsAppIdPlaceholder = '$' + '{{TEAMS_APP_ID}}'
const botIdPlaceholder = '$' + '{{AAD_APP_CLIENT_ID}}'
const namePlaceholder = '$' + '{{NAME}}'

describe('manifest validation', () => {
  it('rejects the legacy client ID variable while retaining unrelated placeholders', async () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-identity-'))
    try {
      const packageRoot = path.join(root, 'manifest')
      mkdirSync(packageRoot)
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      const manifest = {
        $schema: 'https://developer.microsoft.com/json-schemas/teams/v1.23/MicrosoftTeams.schema.json',
        manifestVersion: '1.23',
        version: '1.0.0',
        id: '$' + '{{BOT_ID}}',
        name: { short: '$' + '{{APP_NAME_SUFFIX}}' },
        description: {
          short: 'Sample',
          full: 'Sample'
        },
        icons: {
          color: 'color.png',
          outline: 'outline.png'
        },
      }
      const target = {
        distribution: 'sample',
        packageDirectory: 'manifest',
        placeholderConvention: 'toolkit'
      }
      const file = path.join(packageRoot, 'manifest.json')
      writeFileSync(file, JSON.stringify(manifest))
      assert.ok((await checkManifest(root, target, async () => ({ type: 'object' }))).some((error) => error.includes('application client ID placeholder')))
      manifest.id = '$' + '{{AAD_APP_CLIENT_ID}}'
      writeFileSync(file, JSON.stringify(manifest))
      assert.deepEqual(await checkManifest(root, target, async () => ({ type: 'object' })), [])
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('rejects manifest commands missing or inconsistent with explicit README claims', async () => {
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-manifest-'))
    try {
      const packageRoot = path.join(sampleRoot, 'manifest')
      mkdirSync(packageRoot)
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      writeFileSync(
        path.join(sampleRoot, 'README.md'),
        'The app manifest declares `my-reminders` as a slash command, `remind` as a mention command, and `reminder-help` for both command surfaces.\n'
      )
      writeFileSync(
        path.join(packageRoot, 'manifest.json'),
        JSON.stringify(
          {
            $schema: 'https://developer.microsoft.com/json-schemas/teams/v1.23/MicrosoftTeams.schema.json',
            manifestVersion: '1.23',
            version: '1.0.0',
            id: teamsAppIdPlaceholder,
            name: { short: 'Commands' },
            description: {
              short: 'Commands',
              full: 'Commands'
            },
            icons: {
              color: 'color.png',
              outline: 'outline.png'
            },
            bots: [{
              botId: botIdPlaceholder,
              scopes: ['personal']
            }],
          },
          null,
          2
        )
      )

      const errors = await checkManifest(
        sampleRoot,
        {
          distribution: 'zip',
          packageDirectory: 'manifest',
          placeholderConvention: namePlaceholder,
        },
        async () => ({ type: 'object' })
      )

      assert.deepEqual(
        errors.filter((error) => error.includes('commandLists')),
        [
          'README says the app manifest declares bot command "my-reminders", but bots[].commandLists does not contain it',
          'README says the app manifest declares bot command "remind", but bots[].commandLists does not contain it',
          'README says the app manifest declares bot command "reminder-help", but bots[].commandLists does not contain it',
        ]
      )

      const manifest = JSON.parse(readFileSync(path.join(packageRoot, 'manifest.json'), 'utf8')) as Record<
        string,
        unknown
      >
      manifest.bots = [
        {
          botId: botIdPlaceholder,
          scopes: ['personal'],
          commandLists: [
            {
              scopes: ['personal'],
              commands: [
                {
                  title: 'my-reminders',
                  description: 'List reminders.'
                },
                {
                  title: 'remind',
                  description: 'Create a reminder.'
                },
                {
                  title: 'reminder-help',
                  description: 'Show help.'
                },
              ],
            },
          ],
        },
      ]
      writeFileSync(path.join(packageRoot, 'manifest.json'), JSON.stringify(manifest))
      const triggerErrors = await checkManifest(
        sampleRoot,
        {
          distribution: 'zip',
          packageDirectory: 'manifest',
          placeholderConvention: namePlaceholder,
        },
        async () => ({ type: 'object' })
      )
      assert.deepEqual(
        triggerErrors.filter((error) => error.includes('triggers')),
        [
          'README requires bot command "my-reminders" triggers [slash], but its commandLists entries have [mention]',
          'README requires bot command "reminder-help" triggers [mention, slash], but its commandLists entries have [mention]',
        ]
      )

      manifest.bots = [
        {
          botId: botIdPlaceholder,
          scopes: ['personal'],
          commandLists: [
            {
              scopes: ['personal'],
              triggers: ['slash'],
              commands: [{
                title: 'my-reminders',
                description: 'List reminders.'
              }],
            },
            {
              scopes: ['personal'],
              commands: [{
                title: 'remind',
                description: 'Create a reminder.'
              }]
            },
            {
              scopes: ['personal'],
              triggers: ['slash', 'mention'],
              commands: [{
                title: 'reminder-help',
                description: 'Show help.'
              }],
            },
          ],
        },
      ]
      writeFileSync(path.join(packageRoot, 'manifest.json'), JSON.stringify(manifest))
      assert.deepEqual(
        await checkManifest(
          sampleRoot,
          {
            distribution: 'zip',
            packageDirectory: 'manifest',
            placeholderConvention: namePlaceholder,
          },
          async () => ({ type: 'object' })
        ),
        []
      )
    } finally {
      rmSync(sampleRoot, {
        recursive: true,
        force: true
      })
    }
  })

  it('returns a repairable error for a noncanonical released schema URL', async () => {
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-schema-url-'))
    try {
      const packageRoot = path.join(sampleRoot, 'manifest')
      mkdirSync(packageRoot)
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      writeFileSync(
        path.join(packageRoot, 'manifest.json'),
        JSON.stringify({
          $schema: 'https://developer.microsoft.com/en-us/json-schemas/teams/v1.19/MicrosoftTeams.schema.json',
          manifestVersion: '1.19',
          version: '1.0.0',
          id: teamsAppIdPlaceholder,
          name: { short: 'Schema' },
          description: {
            short: 'Schema',
            full: 'Schema'
          },
          icons: {
            color: 'color.png',
            outline: 'outline.png'
          },
          bots: [{
            botId: botIdPlaceholder,
            scopes: ['personal']
          }],
        })
      )
      const errors = await checkManifest(
        sampleRoot,
        {
          distribution: 'zip',
          packageDirectory: 'manifest',
          placeholderConvention: namePlaceholder,
        },
        async () => ({ type: 'object' })
      )
      assert.deepEqual(errors, ['Manifest $schema does not match manifestVersion on developer.microsoft.com'])
    } finally {
      rmSync(sampleRoot, {
        recursive: true,
        force: true
      })
    }
  })

  it('requires compose command IDs, types, and link-handler domains to match message-extension routes', async () => {
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-message-extension-'))
    try {
      const packageRoot = path.join(sampleRoot, 'manifest')
      mkdirSync(packageRoot)
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      writeFileSync(
        path.join(sampleRoot, 'MessageExtensionAgent.cs'),
        `
[TeamsQueryRoute("wikipediaSearch")]
public Task Query() => Task.CompletedTask;
[TeamsQueryLinkRoute]
public Task QueryLink() => Task.CompletedTask;
`
      )
      writeFileSync(
        path.join(packageRoot, 'manifest.json'),
        JSON.stringify({
          $schema: 'https://developer.microsoft.com/json-schemas/teams/v1.22/MicrosoftTeams.schema.json',
          manifestVersion: '1.22',
          version: '1.0.0',
          id: teamsAppIdPlaceholder,
          name: { short: 'Extension' },
          description: {
            short: 'Extension',
            full: 'Extension'
          },
          icons: {
            color: 'color.png',
            outline: 'outline.png'
          },
          composeExtensions: [
            {
              commands: [{
                id: 'wrong-id',
                type: 'action'
              }],
              messageHandlers: [{
                type: 'link',
                value: { domains: ['*.wikipedia.org'] }
              }],
            },
          ],
        })
      )
      const errors = await checkManifest(
        sampleRoot,
        {
          distribution: 'zip',
          packageDirectory: 'manifest',
          placeholderConvention: namePlaceholder,
        },
        async () => ({ type: 'object' })
      )
      assert.deepEqual(
        errors.filter((error) => error.includes('TeamsQuery')),
        [
          'TeamsQueryRoute "wikipediaSearch" requires exactly one composeExtensions command with type "query"',
          'TeamsQueryLinkRoute link-handler domains must be exact domains, not wildcards',
        ]
      )
    } finally {
      rmSync(sampleRoot, {
        recursive: true,
        force: true
      })
    }
  })
})

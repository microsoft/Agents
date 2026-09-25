import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { protection, targets } from '../../src/config.js'
import {
  checkManifest,
  checkProject,
  checkRestoredPackages,
  validateSample,
  type ValidationRuntime,
} from '../../src/validate.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const teamsAppIdPlaceholder = '$' + '{{TEAMS_APP_ID}}'
const botIdPlaceholder = '$' + '{{BOT_ID}}'
const namePlaceholder = '$' + '{{NAME}}'

describe('sample validation', () => {
  it('uses each configured sample manifest directory', () => {
    const configured = targets(repo)
    for (const target of Object.values(configured.samples)) {
      assert.equal(target.manifest.packageDirectory, 'manifest')
      assert.ok(
        existsSync(path.join(repo, configured.destinationRoot, target.destination, 'manifest', 'manifest.json'))
      )
    }
  })

  it('runs sample validation commands relative to the sample working directory', async () => {
    const configured = targets(repo)
    const target = configured.samples['agent-targeted-messages']!
    const selectedVersion = `${configured.packagePolicy.minimumAgentsSdkVersion}.*`
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-validation-'))
    try {
      const packageRoot = path.join(sampleRoot, 'manifest')
      mkdirSync(packageRoot)
      const packages = [
        'Microsoft.Agents.Authentication.Msal',
        'Microsoft.Agents.Hosting.AspNetCore',
        'Microsoft.Agents.Extensions.MSTeams',
      ].map((name) => `<PackageReference Include="${name}" Version="${selectedVersion}" />`)
      writeFileSync(
        path.join(sampleRoot, 'AgentTargetedMessages.csproj'),
        [
          '<Project Sdk="Microsoft.NET.Sdk.Web">',
          `<PropertyGroup><TargetFramework>${configured.packagePolicy.targetFramework}</TargetFramework></PropertyGroup>`,
          `<ItemGroup>${packages.join('')}</ItemGroup>`,
          '</Project>',
        ].join('\n')
      )
      writeFileSync(
        path.join(sampleRoot, 'Program.cs'),
        '[TeamsExtension] partial class SampleAgent : AgentApplication {} // AddAgentDefaults AddAgent< UseAgents MapDefaultAgentEndpoints'
      )
      const assetsPath = path.join(sampleRoot, 'obj', 'project.assets.json')
      mkdirSync(path.dirname(assetsPath))
      writeFileSync(
        assetsPath,
        JSON.stringify({ targets: { net10: { 'Microsoft.Agents.Hosting.AspNetCore/9.0.0': {} } } })
      )
      writeFileSync(path.join(packageRoot, 'color.png'), 'icon')
      writeFileSync(path.join(packageRoot, 'outline.png'), 'icon')
      writeFileSync(
        path.join(packageRoot, 'manifest.json'),
        JSON.stringify({
          $schema: 'https://developer.microsoft.com/json-schemas/teams/v1.23/MicrosoftTeams.schema.json',
          manifestVersion: '1.23',
          version: '1.0.0',
          id: teamsAppIdPlaceholder,
          name: { short: 'Sample' },
          description: { short: 'Sample', full: 'Sample' },
          icons: { color: 'color.png', outline: 'outline.png' },
          bots: [{ botId: botIdPlaceholder, scopes: ['personal'] }],
        })
      )
      const commands: Array<{ args: string[]; cwd: string }> = []
      let smokeProject = ''
      const runtime: ValidationRuntime = {
        runCommand: async (_command, args, cwd) => {
          commands.push({ args, cwd })
          if (args[0] === 'restore') {
            assert.equal(existsSync(assetsPath), false, 'stale assets must be removed before restore')
            writeFileSync(
              assetsPath,
              JSON.stringify({
                targets: {
                  net10: Object.fromEntries(
                    packages.map((reference) => {
                      const name = /Include="([^"]+)"/.exec(reference)![1]!
                      return [`${name}/1.8.77`, {}]
                    })
                  ),
                },
              })
            )
          }
          return []
        },
        runHttpSmoke: async (_root, project) => {
          smokeProject = project
          return []
        },
        loadSchema: async () => ({ type: 'object' }),
      }

      const validation = await validateSample(
        repo,
        'agent-targeted-messages',
        sampleRoot,
        configured,
        target.manifest,
        protection(repo).outputDigestExcludes,
        selectedVersion,
        runtime
      )

      assert.equal(validation.passed, true)
      assert.deepEqual(commands[0], { args: ['restore', 'AgentTargetedMessages.csproj', '--nologo'], cwd: sampleRoot })
      assert.deepEqual(commands[1], {
        args: ['build', 'AgentTargetedMessages.csproj', '--no-restore', '--nologo', '--warnaserror'],
        cwd: sampleRoot,
      })
      assert.ok(commands.some((command) => command.args.includes(`-p:AgentsSdkTestVersion=${selectedVersion}`)))
      assert.equal(smokeProject, 'AgentTargetedMessages.csproj')
    } finally {
      rmSync(sampleRoot, { recursive: true, force: true })
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
            description: { short: 'Commands', full: 'Commands' },
            icons: { color: 'color.png', outline: 'outline.png' },
            bots: [{ botId: botIdPlaceholder, scopes: ['personal'] }],
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
                { title: 'my-reminders', description: 'List reminders.' },
                { title: 'remind', description: 'Create a reminder.' },
                { title: 'reminder-help', description: 'Show help.' },
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
              commands: [{ title: 'my-reminders', description: 'List reminders.' }],
            },
            { scopes: ['personal'], commands: [{ title: 'remind', description: 'Create a reminder.' }] },
            {
              scopes: ['personal'],
              triggers: ['slash', 'mention'],
              commands: [{ title: 'reminder-help', description: 'Show help.' }],
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
      rmSync(sampleRoot, { recursive: true, force: true })
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
          description: { short: 'Schema', full: 'Schema' },
          icons: { color: 'color.png', outline: 'outline.png' },
          bots: [{ botId: botIdPlaceholder, scopes: ['personal'] }],
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
      rmSync(sampleRoot, { recursive: true, force: true })
    }
  })

  it('rejects an MSBuild override that restores an older line than the project declares', () => {
    const configured = targets(repo)
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-restore-policy-'))
    try {
      const project = path.join(sampleRoot, 'Sample.csproj')
      const names = [
        'Microsoft.Agents.Authentication.Msal',
        'Microsoft.Agents.Hosting.AspNetCore',
        'Microsoft.Agents.Extensions.MSTeams',
      ]
      writeFileSync(
        project,
        [
          '<Project Sdk="Microsoft.NET.Sdk.Web">',
          `  <PropertyGroup><TargetFramework>${configured.packagePolicy.targetFramework}</TargetFramework></PropertyGroup>`,
          '  <ItemGroup>',
          ...names.map((name) => `    <PackageReference Include="${name}" Version="1.9.*" />`),
          '  </ItemGroup>',
          '  <ItemGroup>',
          ...names.map((name) => `    <PackageReference Update="${name}" Version="1.8.*" />`),
          '  </ItemGroup>',
          '</Project>',
        ].join('\n')
      )
      writeFileSync(
        path.join(sampleRoot, 'Program.cs'),
        '[TeamsExtension] partial class SampleAgent : AgentApplication {} // AddAgentDefaults AddAgent< UseAgents MapDefaultAgentEndpoints'
      )
      assert.deepEqual(checkProject(sampleRoot, configured, '1.9.*').errors, [])

      mkdirSync(path.join(sampleRoot, 'obj'))
      writeFileSync(
        path.join(sampleRoot, 'obj', 'project.assets.json'),
        JSON.stringify({
          targets: {
            net10: Object.fromEntries(names.map((name) => [`${name}/1.8.77`, {}])),
          },
        })
      )
      assert.equal(checkRestoredPackages(project, '1.9.*').length, 3)
    } finally {
      rmSync(sampleRoot, { recursive: true, force: true })
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
          description: { short: 'Extension', full: 'Extension' },
          icons: { color: 'color.png', outline: 'outline.png' },
          composeExtensions: [
            {
              commands: [{ id: 'wrong-id', type: 'action' }],
              messageHandlers: [{ type: 'link', value: { domains: ['*.wikipedia.org'] } }],
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
      rmSync(sampleRoot, { recursive: true, force: true })
    }
  })
})

import assert from 'node:assert/strict'
import {
  existsSync, mkdtempSync, mkdirSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { protection, targets } from '../../src/config.js'
import {
  checkProject,
  checkRestoredPackages,
  validateSample,
  type ValidationRuntime,
} from '../../src/validate.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const teamsAppIdPlaceholder = '$' + '{{TEAMS_APP_ID}}'
const botIdPlaceholder = '$' + '{{AAD_APP_CLIENT_ID}}'

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
    const configured = {
      ...targets(repo),
      contractDirectory: 'alternate-contracts'
    }
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
          description: {
            short: 'Sample',
            full: 'Sample'
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
      const commands: Array<{
        args: string[]
        cwd: string
      }> = []
      let smokeProject = ''
      const runtime: ValidationRuntime = {
        runCommand: async (_command, args, cwd) => {
          commands.push({
            args,
            cwd
          })
          if (args[0] === 'restore') {
            assert.equal(existsSync(assetsPath), false, 'stale assets must be removed before restore')
            writeFileSync(
              assetsPath,
              JSON.stringify({
                targets: {
                  net10: Object.fromEntries(
                    packages.map((reference) => {
                      const name = /Include="([^"]+)"/.exec(reference)![1]!

                      return [`${name}/${selectedVersion.replace('*', '77')}`, {}]
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
      assert.deepEqual(commands[0], {
        args: ['restore', 'AgentTargetedMessages.csproj', '--nologo'],
        cwd: sampleRoot
      })
      assert.deepEqual(commands[1], {
        args: ['build', 'AgentTargetedMessages.csproj', '--no-restore', '--nologo', '--warnaserror'],
        cwd: sampleRoot,
      })
      assert.ok(commands.some((command) => command.args.includes(`-p:AgentsSdkTestVersion=${selectedVersion}`)))
      assert.ok(commands.some((command) => command.args.includes(
        path.join(repo, configured.contractDirectory, 'TeamsSampleSync.ContractTests.csproj')
      )))
      const contracts = commands.find((command) => command.args[0] === 'test')!
      assert.equal(contracts.args[contracts.args.indexOf('--filter') + 1], '(Sample=agent-targeted-messages)|(Category=TeamsHarness)')
      assert.equal(smokeProject, 'AgentTargetedMessages.csproj')
    } finally {
      rmSync(sampleRoot, {
        recursive: true,
        force: true
      })
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
        JSON.stringify({ targets: { net10: Object.fromEntries(names.map((name) => [`${name}/1.8.77`, {}])), }, })
      )
      assert.equal(checkRestoredPackages(project, '1.9.*').length, 3)
    } finally {
      rmSync(sampleRoot, {
        recursive: true,
        force: true
      })
    }
  })
})

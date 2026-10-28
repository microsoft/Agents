import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { targets } from '../../src/config.js'
import { createPlan } from '../../src/plan.js'
import { checkProject } from '../../src/validate.js'
import { resolveAgentsSdkVersion, selectAgentsSdkVersion } from '../../src/versions.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const release = { tag_name: 'v1.9.7', draft: false, prerelease: false }

function feeds (teamsVersions: string[]): Array<{ versions: string[] }> {
  return [
    { versions: ['1.8.77', '1.9.7'] },
    { versions: ['1.8.77', '1.9.7'] },
    { versions: ['1.8.50', ...teamsVersions] },
    { versions: ['1.8.77', '1.9.7'] },
  ]
}

describe('Agents SDK release policy', () => {
  it('selects the newest stable release line shared by the required packages', () => {
    assert.equal(selectAgentsSdkVersion('1.8', [release], feeds(['1.9.3-beta'])).selectedAgentsSdkVersion, '1.8.*')
    assert.deepEqual(selectAgentsSdkVersion('1.8', [release], feeds(['1.9.3'])), {
      minimumAgentsSdkVersion: '1.8',
      selectedAgentsSdkVersion: '1.9.*',
      releaseTag: 'v1.9.7',
    })
    assert.deepEqual(
      selectAgentsSdkVersion(
        '1.8',
        [{ ...release, tag_name: 'v2.0.0' }, { ...release, tag_name: 'v1.9.7-beta', prerelease: true }, release],
        feeds(['1.9.3'])
      ).selectedAgentsSdkVersion,
      '1.9.*'
    )
    assert.throws(
      () => selectAgentsSdkVersion('1.8', [{ ...release, tag_name: 'v2.0.0' }], feeds(['1.9.3'])),
      /No stable Agents SDK release in major 1/
    )
    assert.throws(
      () => selectAgentsSdkVersion('1.9', [release], feeds(['1.9.3-beta'])),
      /No stable Agents SDK release line/
    )
  })

  it('finds a compatible major on a later release-history page', async () => {
    const newer = Array.from({ length: 100 }, (_, patch) => ({
      tag_name: `v2.0.${patch}`,
      draft: false,
      prerelease: false,
    }))
    const requested: string[] = []
    const request = async (input: string | URL | Request): Promise<Response> => {
      const url = String(input)
      requested.push(url)
      const body = url.includes('releases?')
        ? new URL(url).searchParams.get('page') === '1'
          ? newer
          : [release]
        : feeds(['1.9.3'])[
          ['hosting.aspnetcore', 'authentication.msal', 'extensions.msteams', 'builder.testing'].findIndex((name) =>
            url.includes(name)
          )
        ]
      return new Response(JSON.stringify(body), { status: 200 })
    }
    const selected = await resolveAgentsSdkVersion('1.8', request as typeof fetch)
    assert.equal(selected.selectedAgentsSdkVersion, '1.9.*')
    assert.ok(requested.some((url) => url.includes('page=2')))
  })

  it('accepts a newer compatible line and rejects samples below the planned line', () => {
    const configured = targets(repo)
    const sampleRoot = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-package-policy-'))
    const packageNames = [
      'Microsoft.Agents.Authentication.Msal',
      'Microsoft.Agents.Hosting.AspNetCore',
      'Microsoft.Agents.Extensions.MSTeams',
    ]
    const writeProject = (versions: string[]): void => {
      const references = packageNames
        .map((name, index) => `    <PackageReference Include="${name}" Version="${versions[index]}" />`)
        .join('\n')
      writeFileSync(
        path.join(sampleRoot, 'Sample.csproj'),
        [
          '<Project Sdk="Microsoft.NET.Sdk.Web">',
          `  <PropertyGroup><TargetFramework>${configured.packagePolicy.targetFramework}</TargetFramework></PropertyGroup>`,
          '  <ItemGroup>',
          references,
          '  </ItemGroup>',
          '</Project>',
        ].join('\n')
      )
    }
    try {
      writeFileSync(
        path.join(sampleRoot, 'Program.cs'),
        [
          '[TeamsExtension] public partial class SampleAgent : AgentApplication {}',
          '// AddAgentDefaults AddAgent< UseAgents MapDefaultAgentEndpoints',
        ].join('\n')
      )

      writeProject(['1.8.*', '1.8.*', '1.8.*'])
      assert.equal(
        checkProject(sampleRoot, configured, '1.9.*').errors.filter((error) => error.includes('planned')).length,
        3
      )

      writeProject(['1.10.1', '1.10.*', '1.10.3'])
      assert.deepEqual(checkProject(sampleRoot, configured, '1.9.*').errors, [])

      writeProject(['1.10.1', '1.10.*', '1.9.3'])
      assert.match(checkProject(sampleRoot, configured, '1.9.*').errors.join('\n'), /same major.minor line/)

      writeProject(['2.0.*', '2.0.*', '2.0.*'])
      assert.equal(
        checkProject(sampleRoot, configured, '1.9.*').errors.filter((error) => error.includes('stable')).length,
        3
      )
    } finally {
      rmSync(sampleRoot, { recursive: true, force: true })
    }
  })

  it('records the resolved target and schedules work when the target line changes', () => {
    const configured = targets(repo)
    const sample = 'agent-targeted-messages'
    const upstream = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-upstream-'))
    try {
      const sourceRoot = path.join(upstream, configured.upstream.root, configured.samples[sample]!.source)
      mkdirSync(sourceRoot, { recursive: true })
      writeFileSync(path.join(sourceRoot, 'source.cs'), 'class Source {}\n')
      execFileSync('git', ['init', '-q'], { cwd: upstream })
      execFileSync('git', ['config', 'core.autocrlf', 'false'], { cwd: upstream })
      execFileSync('git', ['add', '.'], { cwd: upstream })
      execFileSync(
        'git',
        ['-c', 'user.name=Sample Sync', '-c', 'user.email=sync@example.com', 'commit', '-qm', 'Source snapshot'],
        { cwd: upstream }
      )

      const previous = createPlan(
        repo,
        upstream,
        {
          minimumAgentsSdkVersion: '1.8',
          selectedAgentsSdkVersion: '1.8.*',
          releaseTag: 'v1.8.77',
        },
        sample
      )
      const newer = createPlan(
        repo,
        upstream,
        {
          minimumAgentsSdkVersion: '1.8',
          selectedAgentsSdkVersion: '1.9.*',
          releaseTag: 'v1.9.7',
        },
        sample
      )

      assert.equal(newer.version, 3)
      assert.equal(newer.agentsSdkVersion.minimumAgentsSdkVersion, '1.8')
      assert.equal(newer.agentsSdkVersion.selectedAgentsSdkVersion, '1.9.*')
      assert.notEqual(previous.samples[sample]?.inputDigest, newer.samples[sample]?.inputDigest)
      assert.notEqual(
        previous.samples[sample]?.componentDigests?.packagePolicy,
        newer.samples[sample]?.componentDigests?.packagePolicy
      )
    } finally {
      rmSync(upstream, { recursive: true, force: true })
    }
  })
})

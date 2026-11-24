import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { parse, stringify } from 'yaml'
import { configuration, selectedLanguages, targets } from '../../src/config.js'
import { assertSdkSelection } from '../../src/sdk-policy.js'
import { artifactName, targetId } from '../../src/identity.js'
import type { Configuration } from '../../src/types.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

function withConfiguration (edit: (value: Configuration) => void, check: (root: string) => void): void {
  const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-profiles-'))
  try {
    const directory = path.join(root, 'automation/teams-sample-sync/config')
    mkdirSync(directory, { recursive: true })
    const value = parse(readFileSync(path.join(repo, 'automation/teams-sample-sync/config/targets.yml'), 'utf8')) as Configuration
    edit(value)
    writeFileSync(path.join(directory, 'targets.yml'), stringify(value))
    check(root)
  } finally {
    rmSync(root, {
      recursive: true,
      force: true
    })
  }
}

describe('language profiles and target identity', () => {
  it('keeps production .NET migrations on SDK 1.9 or newer', () => {
    const configured = targets(repo, 'dotnet')
    assert.equal(configured.packagePolicy.minimumAgentsSdkVersion, '1.9')
    assert.throws(() => assertSdkSelection(configured, {
      minimumAgentsSdkVersion: '1.9',
      selectedAgentsSdkVersion: '1.8.*',
      releaseTag: 'v1.8.77'
    }), /incompatible/)
    assert.doesNotThrow(() => assertSdkSelection(configured, {
      minimumAgentsSdkVersion: '1.9',
      selectedAgentsSdkVersion: '1.9.*',
      releaseTag: 'v1.9.7'
    }))
  })

  it('resolves independent profiles for the same sample name', () => {
    withConfiguration((value) => {
      const dotnet = value.profiles.dotnet!
      value.profiles.js = {
        ...dotnet,
        destinationRoot: 'samples/nodejs/teams',
        packagePolicy: {
          minimumAgentsSdkVersion: '1.9.1',
          nodeVersion: '24'
        },
      }
    }, (root) => {
      assert.deepEqual(selectedLanguages(root), ['dotnet', 'js'])
      assert.equal(targets(root, 'dotnet').destinationRoot, 'samples/dotnet/teams')
      assert.equal(targets(root, 'js').destinationRoot, 'samples/nodejs/teams')
      const dotnet = {
        language: 'dotnet',
        sample: 'bot-cards'
      } as const
      const js = {
        language: 'js',
        sample: 'bot-cards'
      } as const
      assert.notEqual(targetId(dotnet), targetId(js))
      assert.equal(artifactName(js), 'teams-sample-sync-js--bot-cards')
    })
  })

  it('rejects destinations that overlap across languages', () => {
    withConfiguration((value) => {
      const dotnet = value.profiles.dotnet!
      value.profiles.js = {
        ...dotnet,
        destinationRoot: dotnet.destinationRoot + '/bot-cards',
        packagePolicy: {
          minimumAgentsSdkVersion: '1.9.1',
          nodeVersion: '24'
        },
      }
    }, (root) => assert.throws(() => configuration(root), /Overlapping destination/))
  })

  it('rejects overlapping samples within one profile', () => {
    withConfiguration((value) => {
      value.profiles.dotnet!.samples['bot-cards']!.destination = 'bot-meetings/nested'
    }, (root) => assert.throws(() => configuration(root), /Overlapping destination/))
  })

  it('rejects unsafe paths and unsupported language selections', () => {
    withConfiguration((value) => {
      value.profiles.dotnet!.samples['bot-cards']!.source = '../escape'
    }, (root) => assert.throws(() => configuration(root), /unsafe path/))
    assert.throws(() => selectedLanguages(repo, 'python'), /Unsupported language/)
    assert.throws(() => targetId({
      language: 'js',
      sample: '../escape'
    }), /Invalid sample name/)
  })
})

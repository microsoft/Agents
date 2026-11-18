import assert from 'node:assert/strict'
import {
  existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
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
  it('registers seven Python targets and 21 isolated targets across the three languages', () => {
    const configured = configuration(repo)
    const python = targets(repo, 'python')
    assert.equal(Object.keys(python.samples).length, 7)
    assert.equal(Object.values(configured.profiles).reduce((count, profile) => count + Object.keys(profile.samples).length, 0), 21)

    for (const [sample, target] of Object.entries(python.samples)) {
      assert.equal(target.source, `${sample}/python/${sample}`)
      assert.equal(target.destination, sample)
      assert.ok(existsSync(path.join(repo, python.contractDirectory, `${sample}.py`)))
      assert.equal(selectedLanguages(repo).filter((language) => targets(repo, language).samples[sample]).length, 3)
    }
  })

  it('parses an explicit Python runtime without falling through to Node policy', () => {
    withConfiguration((value) => {
      value.profiles.python = {
        ...value.profiles.dotnet!,
        destinationRoot: 'samples/python/teams',
        packagePolicy: {
          minimumAgentsSdkVersion: '1.9.0',
          pythonVersion: '3.11.14',
        },
      }
    }, (root) => {
      const python = targets(root, 'python')
      assert.equal(python.packagePolicy.pythonVersion, '3.11.14')
      assert.equal(python.packagePolicy.nodeVersion, undefined)
      assert.equal(targetId({
        language: 'python',
        sample: 'bot-cards'
      }), 'python--bot-cards')
      assert.deepEqual(selectedLanguages(root), ['dotnet', 'js', 'python'])
    })
  })

  it('rejects a moving Python runtime alias', () => {
    withConfiguration((value) => {
      value.profiles.python = {
        ...value.profiles.dotnet!,
        destinationRoot: 'samples/python/teams',
        packagePolicy: {
          minimumAgentsSdkVersion: '1.9.0',
          pythonVersion: '3.x'
        },
      }
    }, (root) => assert.throws(() => configuration(root), /Python synchronization runtime/))
  })

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
      assert.deepEqual(selectedLanguages(root), ['dotnet', 'js', 'python'])
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
    assert.throws(() => selectedLanguages(repo, 'ruby'), /Unsupported language/)
    assert.throws(() => targetId({
      language: 'js',
      sample: '../escape'
    }), /Invalid sample name/)
  })
})

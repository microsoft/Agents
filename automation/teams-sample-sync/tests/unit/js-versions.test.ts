import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  assertJsSdkSelection,
  JS_SDK_PACKAGES,
  resolveJsSdkVersions,
  selectJsSdkVersions,
} from '../../src/js-versions.js'

const express = '@microsoft/agents-hosting-express'
const teams = '@microsoft/agents-hosting-extensions-msteams'
const hosting = '@microsoft/agents-hosting'
const activity = '@microsoft/agents-activity'

function feeds (): Record<string, {
  name: string
  versions: Record<string, unknown>
}> {
  const result = Object.fromEntries(JS_SDK_PACKAGES.map((name) => [name, {
    name,
    versions: {
      '1.9.1': {
        version: '1.9.1',
        engines: { node: '>=22' }
      },
      '1.10.0-beta.1': { version: '1.10.0-beta.1' },
      '2.0.0': { version: '2.0.0' },
    } as Record<string, unknown>,
  }]))
  result[express]!.versions['1.9.3'] = {
    version: '1.9.3',
    dependencies: { [hosting]: '^1.9.2' }
  }
  result[teams]!.versions['1.9.4'] = {
    version: '1.9.4',
    dependencies: {
      [hosting]: '1.9.2',
      [activity]: '1.9.1'
    },
  }
  result[hosting]!.versions['1.9.2'] = {
    version: '1.9.2',
    dependencies: { [activity]: '1.9.1' }
  }

  return result
}

describe('JS SDK npm version policy', () => {
  it('freezes compatible stable versions without assuming matching patch numbers', () => {
    const selected = selectJsSdkVersions('1.9.1', feeds())
    assert.deepEqual(selected.packages, {
      [express]: '1.9.3',
      [teams]: '1.9.4',
      [hosting]: '1.9.2',
      [activity]: '1.9.1',
    })
    assert.equal(selected.selectedAgentsSdkVersion, '1.9.2')
    assert.equal(selected.releaseTag, 'npm:1.9.2')
    assert.doesNotThrow(() => assertJsSdkSelection(selected, '1.9.1'))
  })

  it('falls back when the newest host cannot satisfy required dependencies', () => {
    const metadata = feeds()
    metadata[express]!.versions['1.9.5'] = {
      version: '1.9.5',
      dependencies: { [hosting]: '1.9.5' }
    }
    assert.equal(selectJsSdkVersions('1.9.1', metadata).packages![express], '1.9.3')
  })

  it('checks peer requirements and the configured Node runtime', () => {
    const metadata = feeds()
    metadata[express]!.versions['1.9.5'] = {
      version: '1.9.5',
      peerDependencies: { [hosting]: '1.9.5' }
    }
    metadata[express]!.versions['1.9.6'] = {
      version: '1.9.6',
      engines: { node: '>=26' }
    }
    assert.equal(selectJsSdkVersions('1.9.1', metadata).packages![express], '1.9.3')
  })

  it('rejects unavailable floors, malformed feeds and altered selections', () => {
    assert.throws(() => selectJsSdkVersions('1.9', feeds()), /exact stable version/)
    assert.throws(() => selectJsSdkVersions('1.10.0', feeds()), /No compatible stable JS SDK/)
    const incomplete = feeds()
    delete incomplete[teams]
    assert.throws(() => selectJsSdkVersions('1.9.1', incomplete), /must be a mapping/)
    const selected = selectJsSdkVersions('1.9.1', feeds())
    assert.throws(() => assertJsSdkSelection({
      ...selected,
      releaseTag: 'npm:2.0.0'
    }, '1.9.1'), /incompatible/)
    assert.throws(() => assertJsSdkSelection({
      ...selected,
      packages: { [hosting]: '1.9.2' }
    }, '1.9.1'), /incompatible/)
  })

  it('fetches npm metadata without forwarding GitHub authorization', async () => {
    const metadata = feeds()
    const requests: string[] = []
    const request: typeof fetch = async (input, init) => {
      const url = new URL(String(input))
      requests.push(url.href)
      assert.equal(url.hostname, 'registry.npmjs.org')
      assert.equal(new Headers(init?.headers).get('Authorization'), null)

      return new Response(JSON.stringify(metadata[decodeURIComponent(url.pathname.slice(1))]))
    }
    const selected = await resolveJsSdkVersions('1.9.1', request)
    assert.equal(requests.length, JS_SDK_PACKAGES.length)
    assert.equal(selected.packages![hosting], '1.9.2')
  })
})

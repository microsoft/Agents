// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { resolvePythonSdkVersions, selectPythonSdkVersions } from '../../src/python-versions.js'

const runtime = {
  repo: process.cwd(),
  interpreter: 'missing-trusted-python-interpreter',
  pythonVersion: '3.11.9',
}

describe('python-versions', () => {
  it('Python SDK metadata transport rejects HTTP failure before invoking policy', async () => {
    const request = (async (url: string | URL | Request, options?: RequestInit) => {
      assert.equal(String(url), 'https://pypi.org/pypi/microsoft-agents-activity/json')
      assert.equal(options?.redirect, 'error')
      assert.ok(options?.signal)

      return new Response('unavailable', { status: 503 })
    }) as typeof fetch
    await assert.rejects(resolvePythonSdkVersions(runtime, '1.9.0', request), /HTTP 503/)
  })

  it('Python SDK metadata transport rejects oversized streamed responses', async () => {
    let cancelled = false
    const stream = new ReadableStream<Uint8Array>({
      start (controller) {
        controller.enqueue(new Uint8Array(8 * 1024 * 1024 + 1))
      },
      cancel () {
        cancelled = true
      },
    })
    const request = (async () => new Response(stream)) as typeof fetch
    await assert.rejects(resolvePythonSdkVersions(runtime, '1.9.0', request), /size limit/)
    assert.equal(cancelled, true)
  })

  it('Python SDK policy reports a missing trusted interpreter as infrastructure failure', () => {
    assert.throws(() => selectPythonSdkVersions(runtime, '1.9.0', {}), /Cannot execute trusted Python tool/)
  })
})

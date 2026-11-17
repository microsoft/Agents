// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { describe, it } from 'node:test'
import { preparePythonEnvironment, PythonDependencyError } from '../../src/python-environment.js'
import type { ValidationRuntime } from '../../src/validate.js'

describe('python-environment', () => {
  it('Python preparation rejects mismatched environment evidence and cleans its allocation', async () => {
    let allocated = ''
    const runtime: ValidationRuntime = {
      runCommand: (_command, args) => {
        const request = JSON.parse(readFileSync(args[2]!, 'utf8'))
        allocated = path.dirname(request.environmentRoot)
        writeFileSync(args[3]!, JSON.stringify({
          interpreter: '/candidate/python',
          lock: 'forged',
        }))

        return []
      },
      runHttpSmoke: async () => [],
    }
    await assert.rejects(preparePythonEnvironment({
      repo: process.cwd(),
      interpreter: 'trusted-python',
    }, runtime, 'packaging==26.2', {}), /mismatched environment/)
    assert.equal(existsSync(allocated), false)
  })

  it('Python preparation command failure cleans its allocation without accepting a result', async () => {
    let allocated = ''
    const runtime: ValidationRuntime = {
      runCommand: (_command, args) => {
        allocated = path.dirname(args[2]!)

        return ['wheel resolution failed']
      },
      runHttpSmoke: async () => [],
    }
    await assert.rejects(preparePythonEnvironment({
      repo: process.cwd(),
      interpreter: 'trusted-python',
    }, runtime, 'packaging==26.2', {}), PythonDependencyError)
    assert.equal(existsSync(allocated), false)
  })
})

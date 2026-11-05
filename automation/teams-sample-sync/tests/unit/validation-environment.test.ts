import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sanitizedChildEnvironment } from '../../src/validation-process.js'

describe('validation-environment', () => {
  it('retains Windows NuGet directories without forwarding credentials', () => {
    const paths = ['APPDATA', 'LOCALAPPDATA', 'ProgramFiles', 'ProgramFiles(x86)', 'ProgramW6432']
    const keys = [...paths, 'GITHUB_TOKEN', 'AZURE_CLIENT_SECRET']
    const original = new Map(keys.map((key) => [key, process.env[key]]))
    try {
      for (const key of keys) {
        process.env[key] = `fixture-${key}`
      }

      const environment = sanitizedChildEnvironment()
      for (const key of paths) {
        assert.equal(environment[key], `fixture-${key}`)
      }

      assert.equal(environment.GITHUB_TOKEN, undefined)
      assert.equal(environment.AZURE_CLIENT_SECRET, undefined)
    } finally {
      for (const [key, value] of original) {
        if (value === undefined) {
          delete process.env[key]
        } else {
          process.env[key] = value
        }
      }
    }
  })
})

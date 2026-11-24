import assert from 'node:assert/strict'
import {
  chmodSync, existsSync, mkdirSync, mkdtempSync, statSync, symlinkSync, writeFileSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeFixtureDirectory } from '../fixtures/temporary-directory.js'

describe('temporary-directory', () => {
  it('fixture cleanup removes nested read-only snapshots and rejects the temporary root', () => {
    const root = mkdtempSync(path.join(tmpdir(), 'teams-sync-cleanup-'))
    const snapshot = path.join(root, 'snapshot')
    const nested = path.join(snapshot, 'nested')
    mkdirSync(nested, { recursive: true })
    const file = path.join(nested, 'source.txt')
    writeFileSync(file, 'read-only source')
    chmodSync(file, 0o444)
    chmodSync(nested, 0o555)
    chmodSync(snapshot, 0o555)
    try {
      assert.throws(() => removeFixtureDirectory(tmpdir()), /dedicated fixture directory/)
      assert.doesNotThrow(() => removeFixtureDirectory(root))
      assert.equal(existsSync(root), false)
    } finally {
      removeFixtureDirectory(root)
    }
  })

  it('fixture cleanup does not follow a link or change permissions outside its root', () => {
    const root = mkdtempSync(path.join(tmpdir(), 'teams-sync-cleanup-'))
    const outside = mkdtempSync(path.join(tmpdir(), 'teams-sync-outside-'))
    const file = path.join(outside, 'keep.txt')
    writeFileSync(file, 'external file')
    chmodSync(file, 0o444)
    const mode = statSync(file).mode
    try {
      symlinkSync(outside, path.join(root, 'linked'), process.platform === 'win32' ? 'junction' : 'dir')
      removeFixtureDirectory(root)
      assert.equal(existsSync(file), true)
      assert.equal(statSync(file).mode, mode)
    } finally {
      removeFixtureDirectory(root)
      removeFixtureDirectory(outside)
    }
  })
})

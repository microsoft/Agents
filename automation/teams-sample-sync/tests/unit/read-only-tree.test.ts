import assert from 'node:assert/strict'
import { chmodSync, existsSync, mkdirSync, mkdtempSync, statSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { removeReadOnlyTree } from '../../src/read-only-tree.js'

describe('read-only-tree', () => {
  it('owned read-only snapshot trees can be removed before replacement', () => {
    const root = mkdtempSync(path.join(tmpdir(), 'teams-sync-snapshot-'))
    const nested = path.join(root, 'previous')
    mkdirSync(nested)
    const file = path.join(nested, 'source.txt')
    writeFileSync(file, 'source')
    chmodSync(file, 0o444)
    chmodSync(nested, 0o555)
    chmodSync(root, 0o555)
    try {
      assert.doesNotThrow(() => removeReadOnlyTree(root))
      assert.equal(existsSync(root), false)
    } finally {
      removeReadOnlyTree(root)
    }
  })

  it('snapshot removal does not follow a link or chmod its target', () => {
    const root = mkdtempSync(path.join(tmpdir(), 'teams-sync-snapshot-'))
    const outside = mkdtempSync(path.join(tmpdir(), 'teams-sync-outside-'))
    const file = path.join(outside, 'keep.txt')
    writeFileSync(file, 'outside')
    chmodSync(file, 0o444)
    const mode = statSync(file).mode
    try {
      symlinkSync(outside, path.join(root, 'linked'), process.platform === 'win32' ? 'junction' : 'dir')
      removeReadOnlyTree(root)
      assert.equal(existsSync(file), true)
      assert.equal(statSync(file).mode, mode)
    } finally {
      removeReadOnlyTree(root)
      removeReadOnlyTree(outside)
    }
  })
})

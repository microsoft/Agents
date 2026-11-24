import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, rmSync, symlinkSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { digestDirectory } from '../../src/git.js'
import { sourceFiles } from '../../src/source-files.js'

describe('generated directory traversal', () => {
  it('prunes dependency trees before inspecting links and keeps lockfiles in the digest', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-generated-'))
    try {
      writeFileSync(path.join(root, 'package-lock.json'), '{"lockfileVersion":3}')
      writeFileSync(path.join(root, 'index.ts'), 'export const answer = 42\n')
      const excludes = ['node_modules/**', 'dist/**']
      const initial = digestDirectory(root, excludes)
      const dependency = path.join(root, 'node_modules', 'dependency')
      mkdirSync(dependency, { recursive: true })
      // A junction works on Windows without developer-mode symlink privileges.
      symlinkSync(root, path.join(dependency, 'cycle'), 'junction')
      mkdirSync(path.join(root, 'dist'))
      writeFileSync(path.join(root, 'dist', 'index.js'), 'compiled output')

      assert.equal(digestDirectory(root, excludes), initial)
      assert.deepEqual(sourceFiles(root).map((file) => path.basename(file)).sort(), ['index.ts', 'package-lock.json'])
      writeFileSync(path.join(root, 'package-lock.json'), '{"lockfileVersion":3,"packages":{}}')
      assert.notEqual(digestDirectory(root, excludes), initial)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('continues rejecting links in included source directories', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-source-link-'))
    try {
      symlinkSync(root, path.join(root, 'unexpected'), 'junction')
      assert.throws(() => digestDirectory(root, ['node_modules/**']), /Symlink is not allowed/)
      assert.throws(() => sourceFiles(root), /Symlink is not allowed/)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })

  it('does not turn file exclusions into subtree exclusions', () => {
    const root = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-file-exclusion-'))
    try {
      const sources = path.join(root, 'example.user')
      mkdirSync(sources)
      writeFileSync(path.join(sources, 'Program.cs'), 'original source')
      const initial = digestDirectory(root, ['*.user'])
      writeFileSync(path.join(sources, 'Program.cs'), 'changed source')
      assert.notEqual(digestDirectory(root, ['*.user']), initial)
    } finally {
      rmSync(root, {
        recursive: true,
        force: true
      })
    }
  })
})

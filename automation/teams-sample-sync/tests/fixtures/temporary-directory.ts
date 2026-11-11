import { tmpdir } from 'node:os'
import path from 'node:path'
import { removeReadOnlyTree } from '../../src/read-only-tree.js'

/** Unlock only a dedicated test fixture before removing its read-only snapshots. */
export function removeFixtureDirectory (directory: string): void {
  const root = path.resolve(directory)
  if (path.dirname(root) !== path.resolve(tmpdir()) || !path.basename(root).startsWith('teams-sync-')) {
    throw new Error('Cleanup requires a dedicated fixture directory')
  }
  removeReadOnlyTree(root)
}

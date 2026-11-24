/** Enumerates hand-written source without build outputs. */
import { existsSync, readdirSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'

const GENERATED_DIRECTORIES = new Set(['bin', 'obj', '.git', '.vs', 'node_modules', 'dist', 'coverage'])

export function sourceFiles (root: string): string[] {
  if (!existsSync(root)) {
    return []
  }

  const files: string[] = []
  const visit = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const item = path.join(directory, entry.name)

      if (entry.isSymbolicLink()) {
        throw new SyncError(`Symlink is not allowed: ${item}`)
      }

      if (entry.isDirectory() && !GENERATED_DIRECTORIES.has(entry.name)) {
        visit(item)
      } else if (entry.isFile()) {
        files.push(item)
      }
    }
  }

  visit(root)

  return files
}

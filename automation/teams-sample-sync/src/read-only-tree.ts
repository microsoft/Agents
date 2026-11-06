/** Removes an owned snapshot tree without following links outside it. */
import {
  chmodSync, existsSync, lstatSync, readdirSync, rmSync
} from 'node:fs'
import path from 'node:path'

function unlock (entry: string): void {
  const info = lstatSync(entry)

  if (info.isSymbolicLink()) {
    return
  }

  chmodSync(entry, info.isDirectory() ? 0o700 : 0o600)

  if (info.isDirectory()) {
    for (const name of readdirSync(entry)) {
      unlock(path.join(entry, name))
    }
  }
}

export function removeReadOnlyTree (root: string): void {
  if (existsSync(root)) {
    unlock(root)
  }

  rmSync(root, {
    recursive: true,
    force: true
  })
}

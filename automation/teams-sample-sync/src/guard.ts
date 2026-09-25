/** For Copilot Agents only: migration containment infrastructure. */
/**
 * Enforces the migration containment rules before and after every agent tool operation.
 * It rejects changes outside the selected sample, protected repository paths, mismatched upstream commits,
 * and altered frozen context, preventing a migration job from becoming a general repository writer.
 */
import { existsSync } from 'node:fs'
import { changedPaths, digestDirectory, git, matches, tree } from './git.js'
import { SyncError } from './config.js'

export function assertAgentChanges (repo: string, base: string, sampleRoot: string, protectedPaths: string[]): void {
  if ((git(repo, ['rev-parse', 'HEAD']) as string) !== base) {
    throw new SyncError('Agent changed Git HEAD')
  }
  const normalizedRoot = sampleRoot.replaceAll('\\', '/').replace(/\/$/, '')
  for (const changed of changedPaths(repo, base)) {
    if (
      changed === 'manifest-evidence.md' ||
      changed.endsWith('/manifest-evidence.md') ||
      matches(changed, protectedPaths)
    ) {
      throw new SyncError(`Agent changed protected path: ${changed}`)
    }
    if (!changed.startsWith(`${normalizedRoot}/`)) {
      throw new SyncError(`Agent changed outside selected sample: ${changed}`)
    }
  }
}

export function assertContext (root: string, expectedDigest: string): void {
  if (!existsSync(root)) {
    throw new SyncError('Synchronization context disappeared')
  }
  if (digestDirectory(root) !== expectedDigest) {
    throw new SyncError('Agent changed protected context')
  }
}

export function assertUpstream (upstream: string, commit: string, sourcePath: string, sourceTree: string): void {
  if ((git(upstream, ['rev-parse', 'HEAD']) as string) !== commit) {
    throw new SyncError('Upstream HEAD changed')
  }
  if ((git(upstream, ['status', '--porcelain', '--untracked-files=all']) as string) !== '') {
    throw new SyncError('Upstream worktree changed')
  }
  if (tree(upstream, commit, sourcePath) !== sourceTree) {
    throw new SyncError('Upstream source tree changed')
  }
}

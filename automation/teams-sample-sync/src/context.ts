/** For Copilot Agents only: immutable migration-context infrastructure. */
/**
 * Materializes the exact upstream evidence given to one migration.
 * It records the pinned commit, source changes, previous source snapshot, selected destination, and policy,
 * then makes the context read-only so implementation cannot silently change the plan's input.
 */
import { chmodSync, existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { protection, targets, SyncError } from './config.js'
import { digestDirectory, git, hash, materializeTree, tree, upstreamChanges } from './git.js'
import { readPriorState } from './state.js'
import type { Plan, SourceEvidence, SyncContext } from './types.js'

export function sourceEvidence (
  upstream: string,
  previous: string | null,
  current: string,
  sourcePath: string
): SourceEvidence[] {
  // Initial mode inventories the entire source instead of treating missing history as no work.
  if (!previous) {
    const files = (git(upstream, ['ls-tree', '-r', '--name-only', '-z', current, '--', sourcePath], true) as Buffer)
      .toString('utf8')
      .split('\0')
      .filter(Boolean)
    return files.map((file) => ({
      id: hash(file).slice(7, 23),
      path: file.slice(sourcePath.length + 1),
      diff: 'Initial comparison: read this complete file in the current source snapshot and map its behavior.',
    }))
  }
  const changes = upstreamChanges(upstream, previous, current, sourcePath)
  return changes.flatMap((change) => {
    const paths = [...new Set([change.oldPath, change.newPath].filter((p): p is string => p !== null))]
    const diff = git(upstream, [
      'diff',
      '--no-ext-diff',
      '--no-textconv',
      '--find-renames',
      '--unified=8',
      previous,
      current,
      '--',
      ...paths.map((p) => sourcePath + '/' + p),
    ]) as string
    const sections = diff.split(/(?=^@@ )/m)
    const header = sections.shift()!
    const chunks = sections.length ? sections.map((section) => header + section) : [diff]
    return chunks.map((chunk, index) => ({
      id: hash(JSON.stringify([paths, index, chunk])).slice(7, 23),
      path: change.newPath ?? change.oldPath!,
      diff: chunk,
    }))
  })
}

export interface ContextFiles {
  root: string
  file: string
  digest: string
}

function lockTree (root: string): void {
  if (!existsSync(root)) {
    return
  }
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const item = path.join(root, entry.name)
    if (entry.isDirectory()) {
      lockTree(item)
    } else {
      chmodSync(item, 0o444)
    }
  }
  chmodSync(root, 0o555)
}

export function createContext (repo: string, upstream: string, plan: Plan, sample: string): ContextFiles {
  const configured = targets(repo)
  const entry = plan.samples[sample]
  const target = configured.samples[sample]
  if (!target || !entry?.upstreamCommit || !entry.sourceTree || entry.status !== 'pending') {
    throw new SyncError('Plan does not contain a pending selected sample')
  }
  const upstreamHead = git(upstream, ['rev-parse', 'HEAD']) as string
  if (upstreamHead !== plan.upstreamCommit || upstreamHead !== entry.upstreamCommit) {
    throw new SyncError('Upstream HEAD differs from planned commit')
  }
  const sourcePath = `${configured.upstream.root}/${target.source}`
  if (tree(upstream, upstreamHead, sourcePath) !== entry.sourceTree) {
    throw new SyncError('Planned upstream source tree changed')
  }

  const previousState = readPriorState(repo, sample)
  const previousCommit = previousState?.upstreamCommit ?? null
  if (previousCommit) {
    git(upstream, ['cat-file', '-e', `${previousCommit}^{commit}`])
  }
  const previousTree = previousCommit ? (tree(upstream, previousCommit, sourcePath) ?? null) : null
  const root = path.join(repo, '.sync', 'context')
  rmSync(root, { recursive: true, force: true })
  const previousRoot = path.join(root, 'previous-upstream')
  mkdirSync(previousRoot, { recursive: true })
  if (previousCommit && previousTree) {
    materializeTree(upstream, previousCommit, sourcePath, previousRoot)
  }

  const context: SyncContext = {
    version: 2,
    mode: previousCommit ? 'incremental' : 'initial',
    changes: sourceEvidence(upstream, previousCommit, entry.upstreamCommit, sourcePath),
    skills: { migration: configured.migrationSkill, manifest: configured.manifestSkill },
    sample,
    upstream: {
      repository: configured.upstream.repository,
      sourcePath,
      previousCommit,
      currentCommit: entry.upstreamCommit,
      previousTree,
      currentTree: entry.sourceTree,
      initialImport: previousCommit === null,
      changes: previousCommit ? upstreamChanges(upstream, previousCommit, entry.upstreamCommit, sourcePath) : [],
    },
    paths: {
      previousUpstream: '.sync/context/previous-upstream',
      currentUpstream: path.relative(repo, path.join(upstream, sourcePath)).replaceAll('\\', '/'),
      destination: `${configured.destinationRoot}/${target.destination}`,
    },
    migration: {
      targetFramework: configured.packagePolicy.targetFramework,
      ...plan.agentsSdkVersion,
      canonicalSample: configured.canonicalSample,
    },
    manifest: target.manifest,
    protectedPaths: protection(repo).protectedPaths,
  }
  const file = path.join(root, 'sync-context.json')
  if (Buffer.byteLength(JSON.stringify(context)) > 2_000_000) {
    throw new SyncError('Source evidence exceeds 2 MB; split this migration. No evidence was truncated.')
  }
  writeFileSync(file, `${JSON.stringify(context, null, 2)}\n`, 'utf8')
  lockTree(root)
  return { root, file, digest: digestDirectory(root) }
}

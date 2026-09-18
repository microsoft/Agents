/** For Copilot Agents only: deterministic repository-evidence infrastructure. */
/**
 * Provides deterministic Git, hashing, and tree-materialization primitives for the workflow.
 * Planning uses these functions to pin source/configuration inputs; publishing uses the same digests and
 * changed-path helpers to prove the applied patch is the one that was validated.
 */
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { minimatch } from 'minimatch'
import { SyncError, containedPath, relativePath } from './config.js'
import type { UpstreamChange } from './types.js'

export const hash = (value: string | Buffer): string =>
  `sha256:${createHash('sha256').update(value).digest('hex')}`

function stableValue (value: unknown): unknown {
  if (Array.isArray(value)) return value.map(stableValue)
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value as Record<string, unknown>)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, item]) => [key, stableValue(item)]))
  }
  return value
}

export const stable = (value: unknown): string => JSON.stringify(stableValue(value))

function gitResult (root: string, args: string[], binary: boolean, allowFailure: boolean): string | Buffer | undefined {
  const result = spawnSync('git', ['-C', root, ...args], {
    encoding: binary ? 'buffer' : 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  })
  if (result.status !== 0) {
    if (allowFailure) return undefined
    const stderr = Buffer.isBuffer(result.stderr) ? result.stderr.toString('utf8') : String(result.stderr ?? '')
    const stdout = Buffer.isBuffer(result.stdout) ? result.stdout.toString('utf8') : String(result.stdout ?? '')
    throw new SyncError(`Git failed: ${args.join(' ')}: ${(stderr || stdout || result.error?.message || 'unknown error').trim()}`)
  }
  return binary ? result.stdout as Buffer : String(result.stdout).trim()
}

export function git (root: string, args: string[], binary = false): string | Buffer {
  return gitResult(root, args, binary, false)!
}

export function tryGit (root: string, args: string[], binary = false): string | Buffer | undefined {
  return gitResult(root, args, binary, true)
}

export function tree (root: string, ref: string, source: string): string | undefined {
  return tryGit(root, ['rev-parse', `${ref}:${source}`]) as string | undefined
}

export function matches (relative: string, patterns: string[]): boolean {
  const normalized = relative.replaceAll('\\', '/')
  return patterns.some((pattern) =>
    minimatch(normalized, pattern, { dot: true }) || minimatch(normalized, `**/${pattern}`, { dot: true }))
}

export function digestDirectory (root: string, excludes: string[] = []): string {
  if (!existsSync(root)) return hash('<missing>')
  const files: string[] = []
  const visit = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name)
      if (entry.isSymbolicLink()) throw new SyncError(`Symlink is not allowed: ${file}`)
      if (entry.isDirectory()) visit(file)
      else if (entry.isFile()) files.push(file)
    }
  }
  visit(root)
  const digest = createHash('sha256')
  for (const file of files.sort((a, b) => a.localeCompare(b))) {
    const name = path.relative(root, file).replaceAll('\\', '/')
    if (matches(name, excludes)) continue
    digest.update(name, 'utf8')
    digest.update('\0')
    digest.update(readFileSync(file))
    digest.update('\0')
  }
  return `sha256:${digest.digest('hex')}`
}

export function changedPaths (repo: string, base: string): string[] {
  const tracked = (git(repo, ['diff', '--name-only', '-z', base], true) as Buffer).toString('utf8').split('\0').filter(Boolean)
  const untracked = (git(repo, ['ls-files', '--others', '--exclude-standard', '-z'], true) as Buffer).toString('utf8')
    .split('\0').filter((item) => item !== '' && !item.startsWith('.sync/'))
  return [...new Set([...tracked, ...untracked].map((item) => item.replaceAll('\\', '/')))].sort()
}

function binaryBlob (repo: string, ref: string, file: string | null): boolean {
  if (!file) return false
  const content = tryGit(repo, ['show', `${ref}:${file}`], true) as Buffer | undefined
  return content?.includes(0) ?? false
}

export function upstreamChanges (repo: string, previous: string, current: string, sourceRoot: string): UpstreamChange[] {
  const raw = git(repo, ['diff', '--name-status', '-z', '--find-renames', previous, current, '--', sourceRoot], true) as Buffer
  const fields = raw.toString('utf8').split('\0')
  const result: UpstreamChange[] = []
  const prefix = `${sourceRoot.replaceAll('\\', '/').replace(/\/$/, '')}/`
  const strip = (item: string): string => item.replaceAll('\\', '/').startsWith(prefix) ? item.replaceAll('\\', '/').slice(prefix.length) : item
  for (let index = 0; index < fields.length - 1;) {
    const code = fields[index++]!
    const oldRepositoryPath = fields[index++]!
    if (code.startsWith('R') || code.startsWith('C')) {
      const newRepositoryPath = fields[index++]!
      result.push({
        status: 'renamed',
        oldPath: strip(oldRepositoryPath),
        newPath: strip(newRepositoryPath),
        binary: binaryBlob(repo, previous, oldRepositoryPath) || binaryBlob(repo, current, newRepositoryPath),
      })
      continue
    }
    const status = code.startsWith('A') ? 'added' : code.startsWith('D') ? 'deleted' : 'modified'
    result.push({
      status,
      oldPath: status === 'added' ? null : strip(oldRepositoryPath),
      newPath: status === 'deleted' ? null : strip(oldRepositoryPath),
      binary: binaryBlob(repo, status === 'added' ? current : previous, oldRepositoryPath) ||
        binaryBlob(repo, status === 'deleted' ? previous : current, oldRepositoryPath),
    })
  }
  return result
}

export function materializeTree (repo: string, ref: string, sourceRoot: string, destination: string): void {
  const raw = git(repo, ['ls-tree', '-r', '-z', ref, '--', sourceRoot], true) as Buffer
  const prefix = `${sourceRoot.replaceAll('\\', '/').replace(/\/$/, '')}/`
  for (const entry of raw.toString('utf8').split('\0').filter(Boolean)) {
    const tab = entry.indexOf('\t')
    if (tab < 0) throw new SyncError('Unexpected git ls-tree output')
    const metadata = entry.slice(0, tab).split(' ')
    const repositoryPath = entry.slice(tab + 1).replaceAll('\\', '/')
    const mode = metadata[0]
    if (mode === '120000') throw new SyncError(`Symlink is not allowed in upstream tree: ${repositoryPath}`)
    if (!repositoryPath.startsWith(prefix)) continue
    const relative = repositoryPath.slice(prefix.length)
    if (!relativePath(relative)) throw new SyncError(`Unsafe upstream tree path: ${repositoryPath}`)
    const output = containedPath(destination, relative)
    for (let parent = path.dirname(output); parent !== destination; parent = path.dirname(parent)) {
      if (existsSync(parent) && lstatSync(parent).isSymbolicLink()) throw new SyncError(`Symlink is not allowed: ${parent}`)
    }
    mkdirSync(path.dirname(output), { recursive: true })
    writeFileSync(output, git(repo, ['show', `${ref}:${repositoryPath}`], true) as Buffer)
  }
}

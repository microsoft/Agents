/** Small CLI argument and artifact I/O helpers. */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'

export function parseArgs (items: string[]): Record<string, string> {
  const result: Record<string, string> = {}

  for (let index = 0; index < items.length; index += 2) {
    const option = items[index]
    const value = items[index + 1]

    if (!option?.startsWith('--') || !value || value.startsWith('--')) {
      throw new SyncError(`Invalid argument: ${String(option)}`)
    }

    const key = option.slice(2)

    if (
      ![
        'repo-root',
        'upstream-root',
        'plan',
        'sample',
        'language',
        'output-directory',
        'output',
        'result',
        'base-sha',
        'base-branch',
        'repository',
        'results-directory',
        'run-url',
        'handoff-url',
        'handoff-artifact',
      ].includes(key)
    ) {
      throw new SyncError(`Unknown option: ${option}`)
    }

    if (key in result) {
      throw new SyncError(`Duplicate option: ${option}`)
    }

    result[key] = value
  }

  return result
}

export function required (values: Record<string, string>, name: string): string {
  const value = values[name]

  if (!value) {
    throw new SyncError(`Missing required option: --${name}`)
  }

  return value
}

export function readJson<T> (file: string): T {
  try {
    return JSON.parse(readFileSync(file, 'utf8')) as T
  } catch (error) {
    throw new SyncError(`Cannot read JSON ${file}: ${error instanceof Error ? error.message : String(error)}`)
  }
}

export function writeJson (file: string, value: unknown): void {
  mkdirSync(path.dirname(file), { recursive: true })
  writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
}

export function resolveOption (value: string): string {
  return path.resolve(process.env.INIT_CWD ?? process.cwd(), value)
}

export function emitFailure (report: {
  stderr: string
  annotation: string
}): void {
  process.stderr.write(`${report.stderr}\n`)

  if (process.env.GITHUB_ACTIONS === 'true') {
    process.stdout.write(`${report.annotation}\n`)
  }
}

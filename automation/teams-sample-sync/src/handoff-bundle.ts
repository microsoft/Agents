/** Packages verified sample patches and evidence for a Copilot issue assignment. */
import { copyFileSync, existsSync, mkdirSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import type { SyncResult } from './types.js'

export interface HandoffBundleOptions {
  planFile: string
  resultsDirectory: string
  results: SyncResult[]
  output: string
}

const sampleFiles = [
  'change.patch',
  'sync-result.json',
  'migration-plan.md',
  'self-audit.md',
  'source-context.json',
]

export function prepareHandoffBundle (options: HandoffBundleOptions): void {
  mkdirSync(options.output, { recursive: true })
  copyFileSync(options.planFile, path.join(options.output, 'plan.json'))

  for (const result of options.results) {
    const source = path.join(options.resultsDirectory, `teams-sample-sync-${result.sample}`)
    const destination = path.join(options.output, 'samples', result.sample)
    mkdirSync(destination, { recursive: true })
    for (const name of sampleFiles) {
      const file = path.join(source, name)
      if (!existsSync(file)) {
        throw new SyncError(`Missing handoff file for ${result.sample}: ${name}`)
      }
      copyFileSync(file, path.join(destination, name))
    }
  }
}

/** Validated target identity shared by state, artifacts and matrix jobs. */
import { SyncError } from './errors.js'

export type Language = 'dotnet' | 'js'

export interface TargetIdentity {
  language: Language
  sample: string
}

export function language (value: unknown): Language {
  if (value !== 'dotnet' && value !== 'js') {
    throw new SyncError(`Unsupported language: ${String(value)}`)
  }

  return value
}

export function identity (value: TargetIdentity): TargetIdentity {
  language(value.language)

  if (!/^[a-z0-9][a-z0-9-]*$/.test(value.sample)) {
    throw new SyncError(`Invalid sample name: ${value.sample}`)
  }

  return value
}

export function targetId (value: TargetIdentity): string {
  identity(value)

  return `${value.language}--${value.sample}`
}

export function targetLabel (value: TargetIdentity): string {
  identity(value)

  return `${value.language}/${value.sample}`
}

export function artifactName (value: TargetIdentity): string {
  return `teams-sample-sync-${targetId(value)}`
}

export function sameTarget (left: TargetIdentity, right: TargetIdentity): boolean {
  return targetId(left) === targetId(right)
}

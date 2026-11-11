import type { Language, TargetIdentity } from './identity.js'

/**
 * Shared serializable contracts passed between the CLI, workflow artifacts, validators, and publish job.
 * Keeping these objects explicit makes the plan/result handoff auditable and prevents a publish step from
 * accepting an incomplete or differently configured migration result.
 */
export interface ManifestTarget {
  expectedLinkDomains?: string[]
  distribution: string
  packageDirectory: string
  placeholderConvention: string
}

export interface Target {
  source: string
  destination: string
  manifest: ManifestTarget
}

export interface CopilotConfiguration {
  sdkVersion: string
  runtimeVersion: string
}

export interface PackagePolicy {
  targetFramework?: string
  nodeVersion?: string
  minimumAgentsSdkVersion: string
}

export interface AgentsSdkVersionSelection {
  minimumAgentsSdkVersion: string
  selectedAgentsSdkVersion: string
  releaseTag: string
  packages?: Record<string, string>
}

export interface LanguageProfile {
  destinationRoot: string
  canonicalSample: string
  manifestAssets: string
  migrationSkill: string
  syncSkill: string
  contractDirectory: string
  outputDigestExcludes: string[]
  packagePolicy: PackagePolicy
  samples: Record<string, Target>
}

export interface Configuration {
  version: 2
  upstream: {
    repository: string
    ref: string
    root: string
  }
  manifestSkill: string
  copilot: CopilotConfiguration
  validatorVersion: string
  profiles: Partial<Record<Language, LanguageProfile>>
}

export interface Targets extends LanguageProfile {
  version: 2
  language: Language
  upstream: Configuration['upstream']
  manifestSkill: string
  copilot: CopilotConfiguration
  validatorVersion: string
}

export interface Protection {
  version: 1
  protectedPaths: string[]
  outputDigestExcludes: string[]
}

export interface State extends TargetIdentity {
  version: 3
  upstreamCommit: string
  sourceTree: string
  inputDigest: string
  outputDigest: string
  componentDigests: Record<string, string>
  status: 'verified'
}

export type PlanStatus = 'unchanged' | 'pending' | 'upstream-removed'
export interface PlanSample extends TargetIdentity {
  status: PlanStatus
  upstreamCommit?: string
  sourceTree?: string
  inputDigest?: string
  componentDigests?: Record<string, string>
  changedComponents: string[]
  previousState?: unknown
}

export interface Plan {
  version: 4
  upstreamCommit: string
  agentsSdkVersions: Partial<Record<Language, AgentsSdkVersionSelection>>
  samples: Record<string, PlanSample>
  matrix: Array<TargetIdentity & {
    id: string
    upstreamCommit: string
  }>
  newSampleCandidates: Array<TargetIdentity & { status: 'new-sample-candidate' }>
}

export interface UpstreamChange {
  status: 'added' | 'modified' | 'deleted' | 'renamed'
  oldPath: string | null
  newPath: string | null
  binary: boolean
}

export interface SourceEvidence {
  id: string
  path: string
  diff: string
}

export interface SyncContext extends TargetIdentity {
  version: 3
  mode: 'initial' | 'incremental'
  changes: SourceEvidence[]
  skills: {
    migration: string
    manifest: string
  }
  sample: string
  upstream: {
    repository: string
    sourcePath: string
    previousCommit: string | null
    currentCommit: string
    previousTree: string | null
    currentTree: string
    initialImport: boolean
    changes: UpstreamChange[]
  }
  paths: {
    previousUpstream: string
    currentUpstream: string
    destination: string
  }
  migration: PackagePolicy & AgentsSdkVersionSelection & { canonicalSample: string }
  manifest: ManifestTarget
  protectedPaths: string[]
}

export type CheckStatus = 'passed' | 'failed' | 'skipped' | 'not-run'
export interface ValidationCheck {
  status: CheckStatus
  errors: string[]
}

export interface ValidationResult extends TargetIdentity {
  version: 3
  id: string
  sample: string
  passed: boolean
  repairable: boolean
  outputDigest: string
  group: 'code' | 'manifest' | 'all'
  checks: Record<string, ValidationCheck>
  errors: string[]
  externalValidationRequired: string[]
}

export interface ObservedModel {
  model: string
  reasoningEffort: string
}

export interface SyncResult extends TargetIdentity {
  version: 5
  sample: string
  status: 'updated' | 'no-changes' | 'failed'
  publishable: boolean
  baseSha: string
  previousUpstreamCommit: string | null
  upstreamCommit: string
  upstreamChanges: UpstreamChange[]
  changedComponents: string[]
  copilot: CopilotConfiguration
  observedModels: ObservedModel[]
  sourceTree: string
  sourceContextDigest: string
  inputDigest: string
  componentDigests: Record<string, string>
  planHash?: string
  destinationChanges?: string[]
  outputDigest?: string
  state?: State
  validation?: ValidationResult
  summary?: string
  error?: string
  diagnostics: string[]
}

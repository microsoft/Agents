/** For Copilot Agents only: migration handoff contracts. */
/**
 * Shared serializable contracts passed between the CLI, workflow artifacts, validators, and publish job.
 * Keeping these objects explicit makes the plan/result handoff auditable and prevents a publish step from
 * accepting an incomplete or differently configured migration result.
 */
export interface ManifestTarget { distribution: string; packageDirectory: string; placeholderConvention: string; }
export interface Target { source: string; destination: string; manifest: ManifestTarget; }
export interface CopilotConfiguration { sdkVersion: string; runtimeVersion: string; }
export interface Targets { version: 1; upstream: { repository: string; ref: string; root: string }; destinationRoot: string; canonicalSample: string; migrationSkill: string; manifestSkill: string; copilot: CopilotConfiguration; packagePolicy: { targetFramework: string; agentsSdkVersion: string }; validatorVersion: string; samples: Record<string, Target>; }
export interface Protection { version: 1; protectedPaths: string[]; outputDigestExcludes: string[]; }
export interface State { version: 2; sample: string; upstreamCommit: string; sourceTree: string; inputDigest: string; outputDigest: string; componentDigests: Record<string, string>; status: 'verified'; }
export type PlanStatus = 'unchanged' | 'pending' | 'upstream-removed'
export interface PlanSample { status: PlanStatus; upstreamCommit?: string; sourceTree?: string; inputDigest?: string; componentDigests?: Record<string, string>; changedComponents: string[]; previousState?: unknown; }
export interface Plan { version: 2; upstreamCommit: string; samples: Record<string, PlanSample>; matrix: Array<{ sample: string; upstreamCommit: string }>; newSampleCandidates: Array<{ sample: string; status: 'new-sample-candidate' }>; }
export interface UpstreamChange { status: 'added' | 'modified' | 'deleted' | 'renamed'; oldPath: string | null; newPath: string | null; binary: boolean; }
export interface SourceEvidence { id: string; path: string; diff: string; }
export interface SyncContext { version: 1; mode: 'initial' | 'incremental'; changes: SourceEvidence[]; skills: { migration: string; manifest: string }; sample: string; upstream: { repository: string; sourcePath: string; previousCommit: string | null; currentCommit: string; previousTree: string | null; currentTree: string; initialImport: boolean; changes: UpstreamChange[]; }; paths: { previousUpstream: string; currentUpstream: string; destination: string }; migration: Targets['packagePolicy'] & { canonicalSample: string }; manifest: ManifestTarget; protectedPaths: string[]; }
export type CheckStatus = 'passed' | 'failed' | 'skipped' | 'not-run'
export interface ValidationCheck { status: CheckStatus; errors: string[]; output?: string; }
export interface ValidationResult { version: 2; id: string; sample: string; passed: boolean; repairable: boolean; outputDigest: string; group: 'code' | 'manifest' | 'all'; checks: Record<string, ValidationCheck>; errors: string[]; externalValidationRequired: string[]; }
export interface ObservedModel { model: string; reasoningEffort: string; }
export interface SyncResult { version: 4; sample: string; status: 'updated' | 'no-changes' | 'failed'; publishable: boolean; baseSha: string; previousUpstreamCommit: string | null; upstreamCommit: string; upstreamChanges: UpstreamChange[]; changedComponents: string[]; copilot: CopilotConfiguration; observedModels: ObservedModel[]; sourceTree: string; sourceContextDigest: string; inputDigest: string; componentDigests: Record<string, string>; planHash?: string; destinationChanges?: string[]; outputDigest?: string; state?: State; validation?: ValidationResult; selfAudit?: string; error?: string; diagnostics: string[]; }

/** Language-specific validation behind the shared evidence and manifest gates. */
import type { AgentsSdkVersionSelection, Targets, ValidationCheck } from './types.js'
import type { ValidationRuntime } from './validate.js'

export interface SourceCapabilities {
  errors?: string[]
  bot: boolean
  composeExtensions: boolean
  files?: boolean
  resourceSpecificPermissions?: string[]
  queryCommands: Array<{
    id: string
    evidence: string
  }>
  actionCommands?: Array<{
    id: string
    evidence: string
  }>
  linkHandlerEvidence?: string
}

export interface CodeValidationInput {
  repo: string
  sample: string
  sampleRoot: string
  configured: Targets
  selectedAgentsSdkVersion: string
  selection?: AgentsSdkVersionSelection
  runtime: ValidationRuntime
  group: 'code' | 'manifest' | 'all'
}

export interface LanguageValidation {
  validateCode: (input: CodeValidationInput) => Promise<Record<string, ValidationCheck>>
  capabilities: (sampleRoot: string) => SourceCapabilities
  requiredChecks: (sample: string) => string[]
}

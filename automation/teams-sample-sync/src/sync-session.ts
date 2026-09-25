/** For Copilot Agents only: frozen-plan session infrastructure. */
/**
 * Runs the plan → implementation → validation conversation protocol for one sample.
 * It freezes and hashes the migration plan before granting write access, requires a structured self-audit,
 * allows one deterministic repair pass, and stops the session at the per-sample deadline.
 */
import { writeFileSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import { hash } from './git.js'
import type { ImplementationSession } from './agent-runner.js'
import type { ValidationResult } from './types.js'
import { assertFullValidation } from './validate.js'

export interface MigrationSessionOptions {
  sample: string
  contextFile: string
  output: string
  session: ImplementationSession
  validate: () => Promise<ValidationResult>
  deadlineMs?: number
}
export type MigrationOutcome = 'changed' | 'no-changes'
export interface MigrationSessionResult {
  plan: string
  planHash: string
  selfAudit: string
  outcome: MigrationOutcome
  validation: ValidationResult
  repairPasses: number
}

const RESPONSE_PREVIEW_LIMIT = 400

function responseTail (value: string): string {
  const normalized = value.trim().replace(/\s+/g, ' ')
  if (!normalized) {
    return JSON.stringify('<empty>')
  }
  const preview =
    normalized.length > RESPONSE_PREVIEW_LIMIT ? `…${normalized.slice(-RESPONSE_PREVIEW_LIMIT)}` : normalized
  return JSON.stringify(preview)
}

function hasMigrationPlan (value: string): boolean {
  return /^#{1,6}\s+.*migration\s+plan\b/im.test(value)
}

function outcomeFromSelfAudit (value: string): MigrationOutcome {
  if (!/^##\s+self-audit\b/im.test(value)) {
    throw new SyncError(
      [
        'Implementer response is missing required heading "## Self-audit";',
        `the next full validation pass was not run. Response ended with: ${responseTail(value)}`,
      ].join(' ')
    )
  }
  const outcome = /^Outcome:\s*(changed|no changes required)\s*$/im.exec(value)?.[1]?.toLowerCase()
  if (outcome === 'changed') {
    return 'changed'
  }
  if (outcome === 'no changes required') {
    return 'no-changes'
  }
  throw new SyncError(
    [
      'Implementer self-audit is missing required line "Outcome: changed" or "Outcome: no changes required";',
      `the next full validation pass was not run. Response ended with: ${responseTail(value)}`,
    ].join(' ')
  )
}

export function assertOutcomeMatchesSampleChanges (outcome: MigrationOutcome, changes: string[]): void {
  if (outcome === 'changed' && changes.length === 0) {
    throw new SyncError('Implementer reported changes but the selected sample is unchanged')
  }
  if (outcome === 'no-changes' && changes.length > 0) {
    throw new SyncError('Implementer reported no changes but the selected sample was modified')
  }
}

export async function runMigrationSession (options: MigrationSessionOptions): Promise<MigrationSessionResult> {
  let expired = false
  let rejectDeadline: (error: Error) => void = () => {}
  const deadline = new Promise<never>((_resolve, reject) => {
    rejectDeadline = reject
  })
  const timer = setTimeout(
    () => {
      expired = true
      options.session.abort().catch(() => {})
      rejectDeadline(new SyncError('Per-sample migration deadline exceeded'))
    },
    options.deadlineMs ?? 30 * 60_000
  )
  const bounded = <T>(work: Promise<T>): Promise<T> => Promise.race([work, deadline])
  const completedSelfAudit = async (value: string): Promise<{ selfAudit: string; outcome: MigrationOutcome }> => {
    try {
      return { selfAudit: value, outcome: outcomeFromSelfAudit(value) }
    } catch {}
    let selfAudit: string
    try {
      selfAudit = await bounded(
        options.session.send(
          [
            'Continue autonomously from your previous response. Do not ask for confirmation.',
            'If implementation or verification remains, finish it now using the frozen plan and the available skills',
            'and validation tool; if it is already complete, do not alter files.',
            'Return the terminal Markdown response headed exactly "## Self-audit", followed by "Outcome: changed"',
            'or "Outcome: no changes required" on its own line, the plan reconciliation, and validation status.',
          ].join(' '),
          'Continuing migration implementation'
        )
      )
    } catch (error) {
      options.session.setWriteAccess(false)
      throw error
    }
    try {
      return { selfAudit, outcome: outcomeFromSelfAudit(selfAudit) }
    } catch {}
    options.session.setWriteAccess(false)
    selfAudit = await bounded(
      options.session.send(
        [
          'Format only: reissue your previous completed implementation result as a terminal Markdown response',
          'headed exactly "## Self-audit". On its own next line write exactly either "Outcome: changed" or',
          '"Outcome: no changes required", then preserve the audit details and validation status from your',
          'previous response. Do not inspect files, invoke tools, edit files, or perform additional work.',
        ].join(' '),
        'Repairing self-audit format'
      )
    )
    const outcome = outcomeFromSelfAudit(selfAudit)
    options.session.setWriteAccess(true)
    return { selfAudit, outcome }
  }
  try {
    let plan = await bounded(
      options.session.send(
        [
          'Plan only. Proceed autonomously; do not ask for confirmation.',
          [
            `Read source context ${options.contextFile}, inspect the existing destination sample, and invoke the migration`,
            'and manifest skills. Do not edit any files.',
          ].join(' '),
          [
            "The context's minimumAgentsSdkVersion is a floor, not the version to copy;",
            'selectedAgentsSdkVersion is the resolved target for this run. Check the destination package references',
            'and include an upgrade when they are below the selected line.',
          ].join(' '),
          [
            'Before finalizing, reconcile user-facing capabilities across source handlers, README/help text,',
            'the destination implementation, and the actual manifest JSON. For every command, record its exact title,',
            'slash or mention surface, handler evidence, and current bots[].commandLists status.',
            'Documentation is a requirement to verify, not proof of manifest state.',
          ].join(' '),
          [
            'Return a concise Markdown migration spec headed with "## Migration plan" and one item per source behavior:',
            'upstream path and stable symbol, destination change, Agents-specific behavior to preserve, manifest impact,',
            'and validation expectation. Include every mismatch as a required change.',
          ].join(' '),
        ].join('\n'),
        'Planning migration'
      )
    )
    if (!hasMigrationPlan(plan)) {
      plan = await bounded(
        options.session.send(
          [
            'Continue autonomously from your previous response and complete the migration analysis now.',
            'Do not ask for confirmation. Use the source context, destination files, migration skill, and manifest skill',
            'as needed, but do not edit any files. Return the completed Markdown migration specification headed',
            'exactly "## Migration plan" with one item per source behavior.',
          ].join(' '),
          'Continuing migration planning'
        )
      )
    }
    if (!hasMigrationPlan(plan)) {
      plan = await bounded(
        options.session.send(
          [
            'Format only: reissue your previous completed planning result as a concise Markdown migration',
            'specification headed exactly "## Migration plan" and containing one item per source behavior.',
            'Preserve your findings. Do not inspect files, invoke tools, edit files, or perform additional work.',
          ].join(' '),
          'Repairing migration plan format'
        )
      )
    }
    if (!hasMigrationPlan(plan)) {
      throw new SyncError(
        [
          'Implementer response is missing required heading "## Migration plan" after autonomous continuation',
          `and one format retry. Response ended with: ${responseTail(plan)}`,
        ].join(' ')
      )
    }
    const planHash = hash(plan)
    writeFileSync(path.join(options.output, 'migration-plan.md'), `${plan}\n`, 'utf8')
    options.session.setWriteAccess(true)
    let selfAudit = await bounded(
      options.session.send(
        [
          `Implement the frozen migration plan ${planHash}. Proceed autonomously; do not ask for confirmation.`,
          [
            'Use both skills, edit only the selected sample, and use validate_sample while working. Do not revise the plan.',
            'Use selectedAgentsSdkVersion from the source context for Agents SDK package references;',
            'minimumAgentsSdkVersion is only a lower bound.',
          ].join(' '),
          [
            'Before full validation, re-open the final handlers, README/help text, and manifest JSON and compare',
            'every command by exact title and surface under bots[].commandLists; schema validity alone is insufficient.',
          ].join(' '),
          [
            'When finished, re-read the source context, frozen plan, and final changed files. Return a terminal',
            'Markdown response headed "## Self-audit". On its own next line write exactly either "Outcome: changed"',
            'or "Outcome: no changes required", then account for every plan item and state the validation run.',
          ].join(' '),
        ].join('\n'),
        'Implementing frozen migration plan'
      )
    )
    let formatted = await completedSelfAudit(selfAudit)
    selfAudit = formatted.selfAudit
    let outcome = formatted.outcome
    let validation = await bounded(options.validate())
    if (validation.passed) {
      assertFullValidation(validation, options.sample)
      return { plan, planHash, selfAudit, outcome, validation, repairPasses: 0 }
    }
    if (!validation.repairable) {
      throw new SyncError(`Validation infrastructure failure: ${validation.errors.join('\n')}`)
    }
    selfAudit = await bounded(
      options.session.send(
        [
          `The frozen plan ${planHash} remains unchanged. Proceed autonomously; do not ask for confirmation.`,
          'Repair these deterministic validation failures in the selected sample; do not explain away a semantic mismatch.',
          'Re-open the affected files, make the required correction, run full validation, then return a terminal',
          'Markdown response headed "## Self-audit" with "Outcome: changed" or "Outcome: no changes required"',
          `on its own next line, without revising the plan: ${JSON.stringify(validation.errors)}`,
        ].join(' '),
        'Repairing validation failures'
      )
    )
    formatted = await completedSelfAudit(selfAudit)
    selfAudit = formatted.selfAudit
    outcome = formatted.outcome
    validation = await bounded(options.validate())
    assertFullValidation(validation, options.sample)
    return { plan, planHash, selfAudit, outcome, validation, repairPasses: 1 }
  } finally {
    clearTimeout(timer)
    if (expired) {
      await options.session.abort().catch(() => {})
    }
  }
}

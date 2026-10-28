/** For Copilot Agents only: workflow reporting infrastructure. */
/**
 * Turns structured synchronization results into CI-facing output.
 * It keeps workflow logs concise while producing failure annotations, a run summary, diagnostic references,
 * and the draft pull-request body only after a sample has become publishable.
 */
import type { SyncResult, UpstreamChange } from './types.js'

function safe (value: string): string {
  return value
    .replace(/[\r\n]+/g, ' ')
    .replace(/([\\`*_[\]!|])/g, '\\$1')
    .trim()
}
function code (value: string): string {
  return `\`${safe(value).replaceAll('`', '')}\``
}
function fenced (value: string): string {
  const longest = Math.max(0, ...(value.match(/`+/g) ?? []).map((item) => item.length))
  const delimiter = '`'.repeat(Math.max(3, longest + 1))
  return `${delimiter}text\n${value.trim()}\n${delimiter}`
}
function change (change: UpstreamChange): string {
  const path = change.newPath ?? change.oldPath ?? 'unknown'
  return `- ${change.status}: ${code(path)}${change.binary ? ' (binary)' : ''}`
}
function validation (result: SyncResult): string[] {
  return Object.entries(result.validation?.checks ?? {}).map(
    ([name, check]) =>
      `- ${name}: **${check.status}**${check.errors.length ? ` — ${safe(check.errors.join('; '))}` : ''}`
  )
}

function workflowCommandData (value: string): string {
  return value.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A')
}

function workflowCommandProperty (value: string): string {
  return workflowCommandData(value).replaceAll(':', '%3A').replaceAll(',', '%2C')
}

export function githubErrorAnnotation (title: string, message: string): string {
  return `::error title=${workflowCommandProperty(title)}::${workflowCommandData(message)}`
}

export function failureReport (result: SyncResult): { stderr: string; annotation: string } | undefined {
  if (result.status !== 'failed') {
    return undefined
  }
  const message =
    result.error?.trim() ||
    result.diagnostics.find((item) => item.trim())?.trim() ||
    'Sample synchronization failed without a diagnostic.'
  return {
    stderr: `Sample synchronization failed (${result.sample}): ${message}`,
    annotation: githubErrorAnnotation(`${result.sample} synchronization failed`, message),
  }
}

export function prBody (result: SyncResult): string {
  const outcome =
    result.status === 'no-changes'
      ? [
          '### Outcome',
          '',
          'No sample changes were required. This patch records the verified synchronization state.',
          '',
        ]
      : []
  return [
    '## Teams SDK sample synchronization',
    '',
    `Sample: ${code(result.sample)}`,
    `Teams commit: ${code(result.upstreamCommit)}`,
    `Frozen migration plan: ${code(result.planHash ?? 'not recorded')}`,
    '',
    ...outcome,
    '### Source changes',
    '',
    ...(result.upstreamChanges.length ? result.upstreamChanges.map(change) : ['- Initial tracked synchronization.']),
    '',
    '### Validation',
    '',
    ...validation(result),
    '',
    '### Implementer self-audit',
    '',
    result.selfAudit?.trim() ? fenced(result.selfAudit) : 'No self-audit was captured.',
    '',
    '### External validation',
    '',
    ...(
      result.validation?.externalValidationRequired ?? [
        'Credentialed Teams, Entra, Graph, Azure Bot, and portal behavior.',
      ]
    ).map((item) => `- ${safe(item)}`),
    '',
    'This draft was produced by the sample synchronization workflow. Review the sample behavior and external setup before merge.',
    '',
  ].join('\n')
}

export function workflowSummary (result: SyncResult): string {
  const outcome =
    result.status === 'no-changes'
      ? ['No sample changes were required; only verified synchronization state will be recorded.', '']
      : []
  return [
    `### ${safe(result.sample)}: ${result.status}`,
    '',
    result.error ? safe(result.error) : '',
    '',
    ...outcome,
    `Diagnostic artifact: ${code(`teams-sample-sync-${result.sample}`)}`,
    '',
    ...(result.validation ? ['#### Validation', '', ...validation(result), ''] : []),
    ...(result.diagnostics.length
      ? ['#### Diagnostics', '', ...result.diagnostics.map((item) => `- ${safe(item)}`), '']
      : []),
  ].join('\n')
}

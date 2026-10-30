/** For Copilot Agents only: workflow reporting infrastructure. */
/**
 * Turns structured synchronization results into CI-facing output.
 * It keeps workflow logs concise and builds short issue handoffs from publishable results.
 */
import type { SyncResult } from './types.js'
import { hash, stable } from './git.js'

function safe (value: string): string {
  return value
    .replace(/[\r\n]+/g, ' ')
    .replace(/([\\`*_[\]!|])/g, '\\$1')
    .trim()
}
function code (value: string): string {
  return `\`${safe(value).replaceAll('`', '')}\``
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

export function issueTitle (results: SyncResult[]): string {
  if (results.length === 1) {
    return `Publish Teams sample: ${results[0]!.sample.replaceAll('-', ' ')}`
  }
  if (results.length === 2) {
    return `Publish Teams samples: ${results.map((result) => result.sample.replaceAll('-', ' ')).join(' and ')}`
  }
  return `Publish ${results.length} synced Teams .NET samples`
}

export function handoffId (baseSha: string, results: SyncResult[]): string {
  return hash(stable({
    baseSha,
    samples: results.map((result) => ({
      sample: result.sample,
      upstreamCommit: result.upstreamCommit,
      inputDigest: result.inputDigest,
      outputDigest: result.outputDigest,
    })),
  }))
}

export function handoffIssueBody (
  results: SyncResult[],
  baseSha: string,
  baseBranch: string,
  repository: string,
  runUrl: string,
  handoffUrl: string,
  handoffArtifact: string
): string {
  const runId = /\/actions\/runs\/(\d+)/.exec(runUrl)?.[1]
  return [
    'Sync the Teams samples below into their Agents SDK counterparts. Use the prepared patches and migration evidence as a starting point; make any adjustments needed to complete the work.',
    '',
    `- **Target:** ${code(baseBranch)} (prepared at ${code(baseSha)})`,
    `- **Workflow:** [validation results](${runUrl})`,
    `- **Artifact:** [prepared changes and evidence](${handoffUrl})`,
    ...(runId ? [`- **Download:** ${code(`gh run download ${runId} --repo ${repository} --name ${handoffArtifact}`)}`] : []),
    '',
    '### Samples',
    '',
    ...results.map((result) => {
      const summary = result.summary!.replace(/[\r\n]+/g, ' ').trim()
      return `- **${safe(result.sample)}:** ${summary}`
    }),
    '',
    '### Acceptance criteria',
    '',
    '- Complete the sample sync using the patches, plans, and evidence in the artifact.',
    '- Create a PR linked to this issue and summarize each sample\'s changes (or no changes needed).',
    '- Explain any additional changes, including tests, and why they were needed in the PR description.',
    '- Run relevant validation and include results in the PR description. Report failed, blocked, or incomplete checks, including security checks.',
    '- List remaining manual checks in the PR description, or state that none remain.',
    '',
    `<!-- teams-sample-sync-handoff:${handoffId(baseSha, results)} -->`,
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

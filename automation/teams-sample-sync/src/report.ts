/**
 * Turns structured synchronization results into CI-facing output.
 * It keeps workflow logs concise and builds short issue handoffs from publishable results.
 */
import type { SyncResult } from './types.js'
import { hash, stable } from './git.js'
import { artifactName, targetLabel } from './identity.js'

function escapeMarkdown (value: string): string {
  return value
    .replace(/[\r\n]+/g, ' ')
    .replace(/([\\`*_[\]!|])/g, '\\$1')
    .trim()
}

function inlineCode (value: string): string {
  return `\`${escapeMarkdown(value).replaceAll('`', '')}\``
}

function validationLines (result: SyncResult): string[] {
  return Object.entries(result.validation?.checks ?? {}).map(([name, check]) => {
    const status = `- ${name}: **${check.status}**`

    if (check.errors.length === 0) {
      return status
    }

    return `${status} — ${escapeMarkdown(check.errors.join('; '))}`
  })
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

export function failureReport (result: SyncResult): {
  stderr: string
  annotation: string
} | undefined {
  if (result.status !== 'failed') {
    return undefined
  }

  const message =
    result.error?.trim() ||
    result.diagnostics.find((item) => item.trim())?.trim() ||
    'Sample synchronization failed without a diagnostic.'

  return {
    stderr: `Sample synchronization failed (${result.language}/${result.sample}): ${message}`,
    annotation: githubErrorAnnotation(`${result.language}/${result.sample} synchronization failed`, message),
  }
}

export function issueTitle (results: SyncResult[]): string {
  if (results.length === 1) {
    return `Publish Teams sample: ${targetLabel(results[0]!)}`
  }

  if (results.length === 2) {
    return `Publish Teams samples: ${results.map(targetLabel).join(' and ')}`
  }

  return `Publish ${results.length} synced Teams samples`
}

export function handoffId (baseSha: string, results: SyncResult[]): string {
  return hash(stable({
    baseSha,
    samples: results.map((result) => ({
      sample: result.sample,
      language: result.language,
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
    'This workflow-generated handoff issue authorizes synchronization maintenance of the listed generated samples under the repository Copilot instructions. It does not expand the selected-sample scope or bypass validation and protected-file safeguards.',
    '',
    `- **Target:** ${inlineCode(baseBranch)} (prepared at ${inlineCode(baseSha)})`,
    `- **Workflow:** [validation results](${runUrl})`,
    `- **Artifact:** [prepared changes and evidence](${handoffUrl})`,
    ...(runId ? [`- **Download:** ${inlineCode(`gh run download ${runId} --repo ${repository} --name ${handoffArtifact}`)}`] : []),
    '',
    '### Samples',
    '',
    ...results.map((result) => {
      const summary = result.summary!.replace(/[\r\n]+/g, ' ').trim()

      return `- **${escapeMarkdown(targetLabel(result))}:** ${summary}`
    }),
    '',
    '### Acceptance criteria',
    '',
    '- Complete the sample sync using the patches, plans, and evidence in the artifact.',
    '- Keep the frozen SDK selection and required compatibility migrations together; preserve source, manifest, and contract behavior. Do not downgrade below the selected target, restore obsolete packages, remove capabilities, weaken tests, or suppress diagnostics to pass.',
    '- Create a PR linked to this issue and summarize each sample\'s changes (or no changes needed).',
    '- Explain any additional changes, including tests, and why they were needed in the PR description.',
    '- Run relevant validation and include results in the PR description. Report failed, blocked, or incomplete checks, including security checks.',
    '- After any adjustments, rerun relevant full validation for the final candidate. Keep unresolved failures visible and the PR not ready to merge; do not treat failed migration evidence as verified success.',
    '- List remaining manual checks in the PR description, or state that none remain.',
    '',
    `<!-- teams-sample-sync-handoff:${handoffId(baseSha, results)} -->`,
  ].join('\n')
}

export function workflowSummary (result: SyncResult): string {
  const lines = [
    `### ${escapeMarkdown(targetLabel(result))}: ${result.status}`,
    '',
    result.error ? escapeMarkdown(result.error) : '',
    '',
  ]

  if (result.status === 'no-changes') {
    lines.push('No sample changes were required; only verified synchronization state will be recorded.', '')
  }

  lines.push(`Diagnostic artifact: ${inlineCode(artifactName(result))}`, '')

  if (result.validation) {
    lines.push('#### Validation', '', ...validationLines(result), '')
  }

  if (result.diagnostics.length > 0) {
    const diagnostics = result.diagnostics.map((item) => `- ${escapeMarkdown(item)}`)
    lines.push('#### Diagnostics', '', ...diagnostics, '')
  }

  return lines.join('\n')
}

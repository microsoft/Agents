import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { failureReport, handoffId, handoffIssueBody, issueTitle, workflowSummary } from '../../src/report.js'
import type { SyncResult } from '../../src/types.js'

function result (overrides: Partial<SyncResult> = {}): SyncResult {
  return {
    version: 4,
    sample: 'bot-message-extensions',
    status: 'failed',
    publishable: false,
    baseSha: 'base',
    previousUpstreamCommit: null,
    upstreamCommit: 'upstream',
    upstreamChanges: [],
    changedComponents: [],
    copilot: { sdkVersion: '1.0.7', runtimeVersion: '1.0.83' },
    observedModels: [],
    sourceTree: 'tree',
    sourceContextDigest: 'context',
    inputDigest: 'input',
    componentDigests: {},
    diagnostics: [],
    ...overrides,
  }
}

describe('synchronization reports', () => {
  it('renders a failed synchronization for stderr and GitHub annotations', () => {
    const report = failureReport(
      result({
        sample: 'bot:message,extensions',
        error: 'Only released Teams manifest schema URLs are supported\n100% checked',
      })
    )

    assert.deepEqual(report, {
      stderr:
        'Sample synchronization failed (bot:message,extensions): Only released Teams manifest schema URLs are supported\n100% checked',
      annotation:
        '::error title=bot%3Amessage%2Cextensions synchronization failed::Only released Teams manifest schema URLs are supported%0A100%25 checked',
    })
  })

  it('falls back to diagnostics when a failed result has no primary error', () => {
    assert.deepEqual(failureReport(result({ diagnostics: ['Validation failed'] })), {
      stderr: 'Sample synchronization failed (bot-message-extensions): Validation failed',
      annotation: '::error title=bot-message-extensions synchronization failed::Validation failed',
    })
  })

  it('does not render a failure report for a publishable result', () => {
    assert.equal(failureReport(result({ status: 'updated', publishable: true })), undefined)
  })

  it('workflow summary identifies the uploaded diagnostic artifact', () => {
    assert.match(workflowSummary(result()), /Diagnostic artifact: `teams-sample-sync-bot-message-extensions`/)
  })

  it('escapes every backslash in Markdown-safe diagnostic output', () => {
    const summary = workflowSummary(result({ diagnostics: ['first\\second\\third'] }))
    assert.match(summary, /first\\\\second\\\\third/)
  })

  it('gives Copilot concise context and acceptance criteria while leaving PR ownership to the agent', () => {
    const first = result({ sample: 'bot-cards', summary: 'Added a card action and updated its manifest.' })
    const second = result({ sample: 'bot-meetings', summary: 'Verified that no code changes were required.' })
    const runUrl = 'https://github.com/example/Agents/actions/runs/123'
    const issue = handoffIssueBody(
      [first, second], 'a'.repeat(40), 'main', 'example/Agents', runUrl,
      `${runUrl}/artifacts/999`, 'teams-sample-sync-handoff-123-1'
    )
    assert.equal(issueTitle([first]), 'Publish Teams sample: bot cards')
    assert.match(issue, /bot-cards.*Added a card action/)
    assert.match(issue, /bot-meetings.*no code changes/)
    assert.match(issue, /make any adjustments needed/)
    assert.match(issue, /Acceptance criteria/)
    assert.match(issue, /Create a PR linked to this issue and summarize each sample's changes/)
    assert.match(issue, /Explain any additional changes, including tests, and why they were needed in the PR description/)
    assert.match(issue, /Run relevant validation and include results in the PR description/)
    assert.match(issue, /Report failed, blocked, or incomplete checks, including security checks/)
    assert.match(issue, /List remaining manual checks in the PR description, or state that none remain/)
    assert.match(issue, /gh run download 123 --repo example\/Agents --name teams-sample-sync-handoff-123-1/)
    assert.match(issue, /Target.*main/)
    assert.match(issue, /artifacts\/999/)
    assert.match(issue, /teams-sample-sync-handoff:sha256:/)
    assert.doesNotMatch(issue, /PR title:|pr-body|apply it exactly|only its changes|draft|workflow will|publication checks/)

    const longSummary = 'Updated the card action and verified its manifest. '.repeat(10).trim()
    const longIssue = handoffIssueBody(
      [{ ...first, summary: longSummary }], 'base', 'main', 'example/Agents', runUrl,
      `${runUrl}/artifacts/999`, 'handoff-123'
    )
    assert.equal(longIssue.split('\n').find((line) => line.startsWith('- **bot-cards:** ')), `- **bot-cards:** ${longSummary}`)
  })

  it('preserves summary Markdown without adding escapes and keeps each sample on one line', () => {
    const summary = 'Updated `bots[0].commandLists` and `{{Bot_Domain}}`.\nKept **existing commands** and the path `first\\second`.'
    const issue = handoffIssueBody(
      [result({ sample: 'bot-cards', summary })],
      'base', 'main', 'example/Agents', 'https://github.com/example/Agents/actions/runs/123',
      'https://github.com/example/Agents/actions/runs/123/artifacts/999', 'handoff-123'
    )
    const sampleLine = issue.split('\n').find((line) => line.startsWith('- **bot-cards:** '))

    assert.equal(sampleLine, `- **bot-cards:** ${summary.replace(/\n/g, ' ')}`)
    assert.doesNotMatch(sampleLine!, /\\[`_[\]]/)
  })

  it('distinguishes different verified outputs from the same migration inputs', () => {
    const first = result({ sample: 'bot-cards', outputDigest: 'first' })
    const second = result({ sample: 'bot-cards', outputDigest: 'second' })
    assert.notEqual(handoffId('a'.repeat(40), [first]), handoffId('a'.repeat(40), [second]))
  })
})

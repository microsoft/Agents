import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { publishDraftPr, publishedPrSummary, type GitHubCommand } from '../../src/publish-pr.js'

const branch = 'automation/teams-sample-sync/bot-cards'
const repo = '/selected/repository'
const head = 'a'.repeat(40)
const openPr = {
  number: 42,
  headRefName: branch,
  headRefOid: head,
  url: 'https://github.com/example/Agents/pull/42',
}

function fakeGitHub (responses: unknown[]): { run: GitHubCommand; commands: { arguments_: string[]; repo: string }[] } {
  const commands: { arguments_: string[]; repo: string }[] = []
  const run: GitHubCommand = (arguments_, workingDirectory) => {
    commands.push({ arguments_, repo: workingDirectory })
    if (arguments_[1] === 'list') {
      return JSON.stringify(responses.shift() ?? [])
    }
    return 'https://github.com/example/Agents/pull/42'
  }
  return { run, commands }
}

describe('draft PR publishing', () => {
  it('creates a PR when no open PR exists, even if a closed PR used the branch', () => {
    const gh = fakeGitHub([[], [openPr]])
    assert.deepEqual(publishDraftPr(repo, branch, 'Sync sample', 'pr-body.md', head, gh.run), openPr)
    assert.deepEqual(gh.commands.map((command) => command.arguments_.slice(0, 2)), [
      ['pr', 'list'],
      ['pr', 'create'],
      ['pr', 'list'],
    ])
    assert.ok(gh.commands[1]?.arguments_.includes('--draft'))
    assert.ok(gh.commands.every((command) => command.repo === repo))
  })

  it('edits an existing open PR by number', () => {
    const gh = fakeGitHub([[openPr], [openPr]])
    assert.deepEqual(publishDraftPr(repo, branch, 'Sync sample', 'pr-body.md', head, gh.run), openPr)
    assert.deepEqual(gh.commands[1]?.arguments_.slice(0, 3), ['pr', 'edit', '42'])
    assert.ok(gh.commands.every((command) => command.repo === repo))
  })

  it('fails if creation returns a closed PR URL without opening a PR', () => {
    const gh = fakeGitHub([[], []])
    assert.throws(
      () => publishDraftPr(repo, branch, 'Sync sample', 'pr-body.md', head, gh.run),
      /No open pull request points to the pushed commit/
    )
  })

  it('fails if the open PR points to a different commit', () => {
    const gh = fakeGitHub([[openPr], [{ ...openPr, headRefOid: 'b'.repeat(40) }]])
    assert.throws(
      () => publishDraftPr(repo, branch, 'Sync sample', 'pr-body.md', head, gh.run),
      /No open pull request points to the pushed commit/
    )
  })

  it('renders an existing ready-for-review PR without calling it a draft', () => {
    const gh = fakeGitHub([[openPr], [openPr]])
    const published = publishDraftPr(repo, branch, 'Sync sample', 'pr-body.md', head, gh.run)
    assert.equal(
      publishedPrSummary('bot-cards', published),
      '### bot-cards: published\n\nOpen pull request: [#42](https://github.com/example/Agents/pull/42)\n'
    )
  })
})

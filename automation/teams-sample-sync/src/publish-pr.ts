/** Publishes a verified sample branch and checks that it has an open pull request. */
import { execFileSync } from 'node:child_process'
import { SyncError } from './config.js'

export interface PullRequest {
  number: number
  headRefName: string
  headRefOid: string
  url: string
}

export type GitHubCommand = (arguments_: string[], repo: string) => string

function openPullRequests (repo: string, branch: string, gh: GitHubCommand): PullRequest[] {
  const response = gh(['pr', 'list', '--head', branch, '--state', 'open', '--json', 'number,headRefName,headRefOid,url'], repo)
  const parsed: unknown = JSON.parse(response)
  if (!Array.isArray(parsed)) {
    throw new SyncError('GitHub returned an invalid pull request list')
  }
  return parsed as PullRequest[]
}

export function publishDraftPr (
  repo: string,
  branch: string,
  title: string,
  bodyFile: string,
  expectedHead: string,
  gh: GitHubCommand = (arguments_, cwd) => execFileSync('gh', arguments_, { cwd, encoding: 'utf8' })
): PullRequest {
  const existing = openPullRequests(repo, branch, gh)
  if (existing.length > 1) {
    throw new SyncError(`Multiple open pull requests found for ${branch}`)
  }
  if (existing.length === 0) {
    gh(['pr', 'create', '--draft', '--head', branch, '--title', title, '--body-file', bodyFile], repo)
  } else {
    gh(['pr', 'edit', String(existing[0]!.number), '--title', title, '--body-file', bodyFile], repo)
  }
  const published = openPullRequests(repo, branch, gh)
  if (
    published.length !== 1 ||
    published[0]?.headRefName !== branch ||
    published[0]?.headRefOid !== expectedHead ||
    !published[0]?.url
  ) {
    throw new SyncError(`No open pull request points to the pushed commit for ${branch}`)
  }
  return published[0]
}

export function publishedPrSummary (sample: string, pullRequest: PullRequest): string {
  return `### ${sample}: published\n\nOpen pull request: [#${pullRequest.number}](${pullRequest.url})\n`
}

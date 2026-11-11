#!/usr/bin/env node
/** Entry point for planning, migration and coordinated issue handoff. */
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { selectedLanguages, targets, SyncError } from './config.js'
import { migrateCandidate } from './candidate.js'
import {
  emitFailure,
  parseArgs,
  readJson,
  required,
  resolveOption,
  writeJson
} from './cli-io.js'
import { prepareHandoffBundle } from './handoff-bundle.js'
import { createPlan } from './plan.js'
import { publishableResults } from './publish-bundle.js'
import {
  githubErrorAnnotation,
  handoffId,
  handoffIssueBody,
  issueTitle
} from './report.js'
import { resolveSdkSelection } from './sdk-policy.js'
import { verifyPatch } from './verify-patch.js'
import type { Plan } from './types.js'

export { migrateCandidate } from './candidate.js'

export async function main (argv = process.argv.slice(2)): Promise<number> {
  try {
    const [command, ...rest] = argv

    if (!['plan', 'migrate', 'verify-patch', 'prepare-handoff', 'prepare-issue'].includes(String(command))) {
      throw new SyncError('Expected command: plan, migrate, verify-patch, prepare-handoff, or prepare-issue')
    }

    const values = parseArgs(rest)
    const repo = path.resolve(process.env.INIT_CWD ?? process.cwd(), values['repo-root'] ?? '.')

    if (command === 'plan') {
      const versions: Plan['agentsSdkVersions'] = {}
      const chosenLanguage = values.language ?? 'all'

      for (const language of selectedLanguages(repo, chosenLanguage)) {
        versions[language] = await resolveSdkSelection(targets(repo, language))
      }

      const plan = createPlan(repo, resolveOption(required(values, 'upstream-root')), versions, values.sample, chosenLanguage)
      writeJson(resolveOption(required(values, 'output')), plan)

      return 0
    }

    if (command === 'migrate') {
      return migrateCandidate(repo, values)
    }

    if (command === 'prepare-handoff' || command === 'prepare-issue') {
      const plan = readJson<Plan>(resolveOption(required(values, 'plan')))
      const results = publishableResults(plan, resolveOption(required(values, 'results-directory')), required(values, 'base-sha'))
      const output = resolveOption(required(values, 'output-directory'))
      mkdirSync(output, { recursive: true })

      if (command === 'prepare-handoff') {
        prepareHandoffBundle({
          planFile: resolveOption(required(values, 'plan')),
          resultsDirectory: resolveOption(required(values, 'results-directory')),
          results,
          output,
        })

        return 0
      }

      writeFileSync(
        path.join(output, 'issue-body.md'),
        handoffIssueBody(
          results,
          required(values, 'base-sha'),
          required(values, 'base-branch'),
          required(values, 'repository'),
          required(values, 'run-url'),
          required(values, 'handoff-url'),
          required(values, 'handoff-artifact')
        ),
        'utf8'
      )
      writeFileSync(path.join(output, 'issue-title.txt'), `${issueTitle(results)}\n`, 'utf8')
      writeFileSync(path.join(output, 'handoff-id.txt'), `${handoffId(required(values, 'base-sha'), results)}\n`, 'utf8')

      return 0
    }

    verifyPatch(repo, values)

    return 0
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    emitFailure({
      stderr: message,
      annotation: githubErrorAnnotation('Teams sample synchronization failed', message)
    })

    return 2
  }
}

const entry = process.argv[1] ? path.resolve(process.argv[1]) : undefined

if (entry && import.meta.url === pathToFileURL(entry).href) {
  process.exitCode = await main()
}

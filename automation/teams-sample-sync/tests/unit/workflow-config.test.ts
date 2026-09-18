import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { parse } from 'yaml'
import { record, targets } from '../../src/config.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

describe('workflow configuration', () => {
  it('manual workflow choices match the configured samples', () => {
    const workflow = record(parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')), 'sync workflow')
    const triggers = record(workflow.on, 'sync workflow on')
    const dispatch = record(triggers.workflow_dispatch, 'sync workflow workflow_dispatch')
    const inputs = record(dispatch.inputs, 'sync workflow inputs')
    const sample = record(inputs.sample, 'sync workflow sample input')
    const expected = ['all', ...Object.keys(targets(repo).samples)]

    assert.equal(sample.type, 'choice')
    assert.deepEqual(sample.options, expected)
  })

  it('scheduled runs process all samples and publish draft pull requests', () => {
    const workflow = record(parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')), 'sync workflow')
    const triggers = record(workflow.on, 'sync workflow on')
    const schedule = triggers.schedule as Array<{ cron?: unknown }>
    const jobs = record(workflow.jobs, 'sync workflow jobs')
    const plan = record(jobs.plan, 'sync workflow plan job')
    const publish = record(jobs.publish, 'sync workflow publish job')

    assert.deepEqual(schedule, [{ cron: '0 0 * * 0' }])
    assert.match(JSON.stringify(plan), /github\.event_name == 'schedule' && 'all' \|\| inputs\.sample/)
    assert.match(String(publish.if), /github\.event_name == 'schedule' \|\| inputs\.createPr/)
  })
})

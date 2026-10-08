import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { parse } from 'yaml'
import { record, targets } from '../../src/config.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

describe('workflow configuration', () => {
  it('manual workflow choices match the configured samples', () => {
    const workflow = record(
      parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')),
      'sync workflow'
    )
    const triggers = record(workflow.on, 'sync workflow on')
    const dispatch = record(triggers.workflow_dispatch, 'sync workflow workflow_dispatch')
    const inputs = record(dispatch.inputs, 'sync workflow inputs')
    const sample = record(inputs.sample, 'sync workflow sample input')
    const expected = ['all', ...Object.keys(targets(repo).samples)]

    assert.equal(sample.type, 'choice')
    assert.deepEqual(sample.options, expected)
  })

  it('scheduled runs assign one coordinated handoff to Copilot only after every migration succeeds', () => {
    const workflow = record(
      parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')),
      'sync workflow'
    )
    const triggers = record(workflow.on, 'sync workflow on')
    const schedule = triggers.schedule as Array<{ cron?: unknown }>
    const jobs = record(workflow.jobs, 'sync workflow jobs')
    const plan = record(jobs.plan, 'sync workflow plan job')
    const publish = record(jobs.publish, 'sync workflow publish job')

    assert.deepEqual(schedule, [{ cron: '0 0 * * 0' }])
    assert.match(JSON.stringify(plan), /github\.event_name == 'schedule' && 'all' \|\| inputs\.sample/)
    assert.match(JSON.stringify(plan), /upstream-removed/)
    assert.match(String(publish.if), /github\.event_name == 'schedule' \|\| inputs\.publish/)
    assert.match(String(publish.if), /needs\.migrate\.result == 'success'/)
    assert.equal(publish.strategy, undefined)
    assert.match(JSON.stringify(publish), /prepare-handoff/)
    assert.match(JSON.stringify(publish), /prepare-issue/)
    assert.match(JSON.stringify(publish), /artifact-url/)
    assert.match(JSON.stringify(publish), /COPILOT_ASSIGNMENT_TOKEN/)
    assert.match(JSON.stringify(publish), /copilot-swe-agent\[bot\]/)
    assert.match(JSON.stringify(publish), /agent_assignment/)
    assert.match(JSON.stringify(publish), /current_base.*BASE_SHA/)
    assert.match(JSON.stringify(publish), /handoff-id\.txt/)
    assert.match(JSON.stringify(publish), /existing_number/)
    assert.doesNotMatch(JSON.stringify(publish), /git push origin/)
    assert.doesNotMatch(JSON.stringify(publish), /gh pr |publish-pr/)
    const steps = publish.steps as Array<{ name?: string; id?: string; run?: string; if?: string }>
    const bundle = steps.findIndex((step) => step.name === 'Prepare consolidated handoff')
    const upload = steps.findIndex((step) => step.id === 'handoff')
    const issue = steps.findIndex((step) => step.name === 'Prepare handoff issue')
    const assignment = steps.findIndex((step) => step.name === 'Create or reuse handoff issue and assign Copilot')
    assert.ok(bundle < upload && upload < issue && issue < assignment)
    assert.doesNotMatch(steps[bundle]!.run!, /issue-number|artifact-urls/)
    assert.match(steps[assignment]!.run!, /PATCH[\s\S]*\/assignees/)
    assert.doesNotMatch(JSON.stringify(publish), /pr-body|custom_instructions|final metadata|ready for review|artifact-urls/)
    assert.deepEqual(Object.keys(jobs), ['plan', 'migrate', 'publish'])
    assert.equal(triggers.pull_request_target, undefined)
    assert.equal(triggers.workflow_run, undefined)
    assert.equal(record(record(triggers.workflow_dispatch, 'dispatch').inputs, 'inputs').finalize_pr_number, undefined)
    assert.match(String(plan.if), /github.event_name == 'schedule'/)
    assert.match(String(plan.if), /github.ref == format/)
  })

  it('does not run a post workflow for Copilot PRs', () => {
    assert.equal(existsSync(path.join(repo, '.github/workflows/finalize-teams-sync-pr.yml')), false)
  })

  it('creates an issue without a PAT and leaves Copilot assignment manual', () => {
    const workflow = record(
      parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')),
      'sync workflow'
    )
    const jobs = record(workflow.jobs, 'jobs')
    const publish = record(jobs.publish, 'publish')
    assert.equal(record(publish.permissions, 'publish permissions').issues, 'write')
    for (const name of ['plan', 'migrate']) {
      assert.notEqual(record(record(jobs[name], name).permissions, `${name} permissions`).issues, 'write')
    }
    const steps = publish.steps as Array<{ name?: string; run?: string; env?: unknown; 'continue-on-error'?: boolean }>
    const handoff = steps.find((step) => step.name === 'Create or reuse handoff issue and assign Copilot')!
    const environment = record(handoff.env, 'handoff environment')
    assert.equal(environment.GH_TOKEN, '${{ secrets.COPILOT_ASSIGNMENT_TOKEN || github.token }}')
    assert.equal(environment.COPILOT_ASSIGNMENT_TOKEN, '${{ secrets.COPILOT_ASSIGNMENT_TOKEN }}')
    const script = handoff.run!
    const manualAssignment = script.match(/if \[ -z "\$COPILOT_ASSIGNMENT_TOKEN" \]; then([\s\S]*?)\n\s*fi/)
    assert.ok(manualAssignment)
    assert.match(manualAssignment[1]!, /Handoff issue: \$issue_url/)
    assert.match(manualAssignment[1]!, /Assign Copilot manually/)
    assert.match(manualAssignment[1]!, /exit 0/)
    assert.ok(manualAssignment.index! > script.indexOf('issue_number='))
    assert.ok(manualAssignment.index! < script.indexOf('> .sync/issue/assignment-request.json'))
    assert.doesNotMatch(script, /test -n "\$GH_TOKEN"/)
    assert.notEqual(handoff['continue-on-error'], true)
    assert.match(script, /Issue was not assigned to Copilot'; exit 1/)
  })

  it('rejects a moved base before issue preparation and immediately before Copilot assignment', () => {
    const workflow = record(
      parse(readFileSync(path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8')),
      'sync workflow'
    )
    const publish = record(record(workflow.jobs, 'jobs').publish, 'publish')
    const steps = publish.steps as Array<{ name?: string; run?: string }>
    const script = steps.find((step) => step.name === 'Create or reuse handoff issue and assign Copilot')!.run!
    const checks = [...script.matchAll(
      /current_base="\$\(git ls-remote origin "refs\/heads\/\$BASE_BRANCH" \| cut -f1\)"\n\s*test "\$current_base" = "\$BASE_SHA" \|\| \{[^\n]*exit 1; \}/g
    )]

    assert.equal(checks.length, 2)
    assert.ok(checks[0]!.index < script.indexOf('gh api --paginate'))
    assert.ok(checks[1]!.index > script.indexOf('> .sync/issue/assignment-request.json'))
    const afterFinalCheck = script.slice(checks[1]!.index + checks[1]![0].length).trimStart()
    assert.ok(afterFinalCheck.startsWith(
      'gh api --method POST "repos/$GITHUB_REPOSITORY/issues/$issue_number/assignees"'
    ))
  })

  it('reads the configured upstream ref and downloads each publish artifact once', () => {
    const workflow = record(parse(readFileSync(
      path.join(repo, '.github/workflows/sync-teams-dotnet-samples.yml'), 'utf8'
    )), 'sync workflow')
    const jobs = record(workflow.jobs, 'jobs')
    const steps = record(jobs.plan, 'plan').steps as Array<{
      id?: string
      name?: string
      run?: string
      env?: Record<string, unknown>
      with?: Record<string, unknown>
    }>
    const configuration = steps.find((step) => step.id === 'upstream')
    assert.match(configuration!.run!, /targets\(process.cwd\(\)\).upstream/)
    assert.match(configuration!.run!, /ref=\$\{ref\}/)
    const fetch = steps.find((step) => step.name === 'Fetch upstream metadata without checking out code')!
    assert.equal(fetch.env!.UPSTREAM_REF, '${{ steps.upstream.outputs.ref }}')
    assert.match(fetch.run!, /git init --bare \.sync\/upstream/)
    assert.match(fetch.run!, /fetch --depth=1 origin "\$UPSTREAM_REF"/)
    assert.match(fetch.run!, /update-ref HEAD 'FETCH_HEAD\^\{commit\}'/)
    assert.ok(steps.indexOf(configuration!) < steps.indexOf(fetch))
    assert.equal(steps.some((step) => step.with?.path === '.sync/upstream'), false)

    assert.match(steps.find((step) => step.id === 'plan')!.run!, /--output \.sync\/plan.json/)
    assert.equal(steps.find((step) => step.with?.name === 'teams-sample-sync-plan')!.with!.path, '.sync/plan.json')
    const migrate = record(jobs.migrate, 'migrate')
    const migrateSteps = migrate.steps as Array<{ run?: string; uses?: string; with?: Record<string, unknown> }>
    const planDownload = migrateSteps.find((step) => step.uses?.startsWith('actions/download-artifact@'))
    assert.equal(planDownload!.with!.name, 'teams-sample-sync-plan')
    assert.equal(planDownload!.with!.path, '.sync')
    assert.match(JSON.stringify(migrate), /--plan \.sync\/plan.json/)
    assert.doesNotMatch(JSON.stringify([steps, migrateSteps]), /\$PLAN_FILE/)

    const publish = record(jobs.publish, 'publish')
    const publishSteps = publish.steps as Array<{ uses?: string; with?: Record<string, unknown> }>
    const downloads = publishSteps.filter((step) => step.uses?.startsWith('actions/download-artifact@'))
    assert.equal(downloads.length, 1)
    assert.equal(downloads[0]!.with!.pattern, 'teams-sample-sync-*')
    assert.equal(record(publish.env, 'publish environment').PLAN_FILE, '.sync/results/teams-sample-sync-plan/plan.json')
    assert.doesNotMatch(JSON.stringify(publish), /--plan \.sync\/plan.json/)
  })
})

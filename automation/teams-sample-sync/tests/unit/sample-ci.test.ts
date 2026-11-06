import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { parse } from 'yaml'
import { configuration } from '../../src/config.js'

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

describe('sample-ci', () => {
  it('sample CI explicitly builds nested Teams packages and runs their protected contracts on supported Node', () => {
    const workflow = parse(readFileSync(path.join(repo, '.github/workflows/cd-samples.yml'), 'utf8'))
    const build = workflow.jobs['build-node']
    assert.deepEqual(build.strategy.matrix['node-version'], [22, 'latest'])
    const script = build.steps.find((step: { name: string }) => step.name === 'Build all Node samples').run as string
    assert.match(script, /samples\/nodejs\/\*\/ samples\/nodejs\/teams\/\*\//)
    assert.doesNotMatch(script, /find |\*\*\/|node_modules/)
    assert.match(JSON.stringify(build.steps), /tests\/js-contracts/)
    assert.deepEqual(Object.keys(configuration(repo).profiles), ['dotnet', 'js'])
  })
})

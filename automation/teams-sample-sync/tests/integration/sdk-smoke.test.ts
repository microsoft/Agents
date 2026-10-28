import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { CopilotAgentRunner } from '../../src/agent-runner.js'

describe('Copilot SDK integration', () => {
  it(
    'keeps skills across a frozen plan and implementation turn',
    {
      skip: process.env.TEAMS_SYNC_SDK_SMOKE !== '1',
      timeout: 6 * 60_000,
    },
    async () => {
      const repo = mkdtempSync(path.join(os.tmpdir(), 'teams-sdk-smoke-'))
      const sample = path.join(repo, 'sample')
      const skill = path.join(repo, 'skills', 'smoke')
      const prompts = path.join(repo, 'automation/teams-sample-sync/prompts')
      for (const directory of [sample, skill, prompts]) {
        mkdirSync(directory, { recursive: true })
      }
      writeFileSync(path.join(sample, 'input.txt'), 'input\n')
      writeFileSync(
        path.join(skill, 'SKILL.md'),
        '---\nname: smoke\ndescription: Smoke skill\n---\nUse the word leaf-skill-loaded in your response.\n'
      )
      writeFileSync(
        path.join(prompts, 'agent-prompt.md'),
        "Follow the caller's plan-only and implementation instructions exactly."
      )
      const runner = new CopilotAgentRunner(
        repo,
        'sample',
        { sdkVersion: '1.0.7', runtimeVersion: '1.0.83' },
        path.join(repo, 'agent.log'),
        [],
        [skill]
      )
      let active: Awaited<ReturnType<typeof runner.open>> | undefined
      try {
        active = await runner.open()
        const plan = await active.send(
          'Read sample/input.txt and invoke smoke. Do not edit. Return a short plan containing the skill word.'
        )
        assert.match(plan, /leaf-skill-loaded/i)
        assert.equal(existsSync(path.join(sample, 'output.txt')), false)
        active.setWriteAccess(true)
        const audit = await active.send(
          'Create sample/output.txt containing FIRST. Return a self-audit containing the skill word.'
        )
        assert.match(audit, /leaf-skill-loaded/i)
        assert.equal(readFileSync(path.join(sample, 'output.txt'), 'utf8').trim(), 'FIRST')
      } finally {
        await active?.close()
        await runner.close()
        rmSync(repo, { recursive: true, force: true })
      }
    }
  )
})

import assert from 'node:assert/strict'
import {
  mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync
} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { describe, it } from 'node:test'
import { ToolSet, type PermissionRequest, type SessionConfig } from '@github/copilot-sdk'
import {
  CopilotAgentRunner, permissionFor, type SdkClient, type SdkSession
} from '../../src/agent-runner.js'

const write = (fileName: string): PermissionRequest => ({
  kind: 'write',
  fileName,
  intention: 'test',
  diff: '',
  canOfferSessionApproval: false,
})

describe('Copilot agent runner', () => {
  it('plan-only permission denies writes and implementation permission confines them', () => {
    const repo = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-permissions-'))
    try {
      mkdirSync(path.join(repo, 'sample'))
      writeFileSync(path.join(repo, 'sample', 'file.txt'), 'x')
      assert.equal(permissionFor(repo, 'sample', false, ['validate_sample'], write('sample/file.txt')).kind, 'reject')
      assert.equal(
        permissionFor(repo, 'sample', true, ['validate_sample'], write('sample/new/file.txt')).kind,
        'approve-once'
      )
      assert.equal(permissionFor(repo, 'sample', true, ['validate_sample'], write('outside.txt')).kind, 'reject')
      const read = (target: string): PermissionRequest => ({
        kind: 'read',
        path: target,
        intention: 'Inspect initial import',
      })
      assert.equal(permissionFor(repo, 'sample', false, [], read('sample/src/missing.py')).kind, 'approve-once')
      assert.equal(permissionFor(repo, 'sample', false, [], read('../outside/missing.py')).kind, 'reject')
      assert.equal(permissionFor(repo, 'sample', false, [], read('.git/missing')).kind, 'reject')
    } finally {
      rmSync(repo, {
        recursive: true,
        force: true
      })
    }
  })

  it('implementation sessions use Copilot Auto and one validation tool', async () => {
    const repo = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-runner-'))
    let config: SessionConfig | undefined
    const session: SdkSession = {
      on: (() => () => {}) as SdkSession['on'],
      sendAndWait: async () => undefined,
      abort: async () => {},
      disconnect: async () => {},
    }
    const client: SdkClient = {
      start: async () => {},
      stop: async () => [],
      getStatus: async () => ({
        version: '1.0.83',
        protocolVersion: 1
      }),
      getAuthStatus: async () => ({ isAuthenticated: true }),
      createSession: async (value) => {
        config = value

        return session
      },
    }
    try {
      const prompts = path.join(repo, 'automation/teams-sample-sync/prompts')
      mkdirSync(prompts, { recursive: true })
      writeFileSync(path.join(prompts, 'agent-prompt.md'), 'Task')
      const runner = new CopilotAgentRunner(
        repo,
        'sample',
        {
          sdkVersion: '1.0.7',
          runtimeVersion: '1.0.83'
        },
        path.join(repo, 'agent.log'),
        [],
        [],
        async () => client
      )
      const active = await runner.open([
        {
          name: 'validate_sample',
          description: 'Validate',
          parameters: { type: 'object' },
          handler: async () => ({})
        },
      ])
      assert.ok(config?.onPermissionRequest)
      const denied = await config.onPermissionRequest(write('outside.txt'), { sessionId: 'test' })
      assert.equal(denied.kind, 'reject')
      assert.match(readFileSync(path.join(repo, 'agent.log'), 'utf8'), /Permission denied.*frozen plan/)
      assert.equal(config?.model, 'auto')
      assert.ok(config.systemMessage?.content?.includes(`Repository root: ${repo}`))
      const toolOutput = config?.largeOutput?.outputDirectory
      assert.ok(toolOutput)
      assert.equal(path.relative(repo, toolOutput).startsWith('..'), false)
      writeFileSync(path.join(toolOutput, 'validation.txt'), 'contract failure details')
      assert.equal(permissionFor(repo, 'sample', false, [], {
        kind: 'read',
        path: path.join(toolOutput, 'validation.txt'),
        intention: 'Read validation details'
      }).kind, 'approve-once')
      assert.equal(config?.tools?.length, 1)
      assert.equal(config?.tools?.[0]?.name, 'validate_sample')
      const availableTools = config?.availableTools as ToolSet
      assert.equal(availableTools.toArray().includes('builtin:skill'), true)
      assert.equal(availableTools.toArray().includes('builtin:task'), false)
      await active.close()
      await runner.close()
    } finally {
      rmSync(repo, {
        recursive: true,
        force: true
      })
    }
  })

  it('returns only the final assistant message while keeping the full transcript out of console output', async () => {
    const repo = mkdtempSync(path.join(os.tmpdir(), 'teams-sync-runner-'))
    const handlers = new Map<string, (event: unknown) => void>()
    const status: string[] = []
    const session = {
      on: (name: string, handler: (event: unknown) => void) => {
        handlers.set(name, handler)

        return () => {}
      },
      sendAndWait: async () => {
        handlers.get('assistant.message')?.({ data: { content: 'I am inspecting files.' } })
        handlers.get('assistant.message')?.({ data: { content: '## Migration plan\n- Ready' } })

        return { data: { content: '## Migration plan\n- Ready' } }
      },
      abort: async () => {},
      disconnect: async () => {},
    } as unknown as SdkSession
    const client: SdkClient = {
      start: async () => {},
      stop: async () => [],
      getStatus: async () => ({
        version: '1.0.83',
        protocolVersion: 1
      }),
      getAuthStatus: async () => ({ isAuthenticated: true }),
      createSession: async () => session,
    }
    try {
      const prompts = path.join(repo, 'automation/teams-sample-sync/prompts')
      mkdirSync(prompts, { recursive: true })
      writeFileSync(path.join(prompts, 'agent-prompt.md'), 'Task')
      const logFile = path.join(repo, 'agent.log')
      const runner = new CopilotAgentRunner(
        repo,
        'sample',
        {
          sdkVersion: '1.0.7',
          runtimeVersion: '1.0.83'
        },
        logFile,
        [],
        [],
        async () => client,
        () => {},
        (value) => status.push(value)
      )
      const active = await runner.open()

      assert.equal(await active.send('Plan', 'Planning migration'), '## Migration plan\n- Ready')
      assert.equal(readFileSync(logFile, 'utf8'), 'I am inspecting files.\n\n## Migration plan\n- Ready')
      assert.deepEqual(status, ['[Copilot] Planning migration...\n', '[Copilot] Planning migration complete.\n'])
      handlers.get('tool.execution_start')?.({
        data: {
          toolCallId: 'failed-create',
          toolName: 'create'
        }
      })
      handlers.get('tool.execution_complete')?.({
        data: {
          success: false,
          toolCallId: 'failed-create',
          result: 'not logged',
          error: { message: 'Permission rejected ghp_fixtureSecret123' }
        }
      })
      assert.match(readFileSync(logFile, 'utf8'), /Tool execution failed.*failed-create/)
      assert.match(readFileSync(logFile, 'utf8'), /"tool":"create".*Permission rejected.*REDACTED/)
      assert.doesNotMatch(readFileSync(logFile, 'utf8'), /ghp_fixtureSecret123/)
      assert.doesNotMatch(readFileSync(logFile, 'utf8'), /not logged/)

      await active.close()
      await runner.close()
    } finally {
      rmSync(repo, {
        recursive: true,
        force: true
      })
    }
  })
})

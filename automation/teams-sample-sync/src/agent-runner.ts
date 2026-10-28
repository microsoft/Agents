/** For Copilot Agents only: autonomous migration runtime infrastructure. */
/**
 * Opens the persistent Copilot SDK session used for one selected sample.
 * It exposes read/search/skill tools during planning, enables edits only after the plan is frozen,
 * blocks shell tools, confines writes to the sample root, and records the full transcript as an artifact.
 */
import { appendFileSync, existsSync, readFileSync, realpathSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  CopilotClient,
  RuntimeConnection,
  ToolSet,
  type CopilotSession,
  type PermissionRequest,
  type PermissionRequestResult,
  type SessionConfig,
  type Tool,
} from '@github/copilot-sdk'
import { SyncError } from './config.js'
import type { CopilotConfiguration, ObservedModel } from './types.js'

export interface ImplementationSession {
  send(message: string, phase?: string): Promise<string>
  setWriteAccess(enabled: boolean): void
  close(): Promise<void>
  abort(): Promise<void>
}
export type SdkSession = Pick<CopilotSession, 'sendAndWait' | 'on' | 'disconnect' | 'abort'>
export type SdkClient = Pick<CopilotClient, 'start' | 'stop' | 'getStatus' | 'getAuthStatus'> & {
  createSession(config: SessionConfig): Promise<SdkSession>
}
export type SdkFactory = () => Promise<SdkClient>

export function createCopilotLog (artifact: (value: string) => void) {
  let hasMessage = false
  return {
    write (value: string): void {
      if (hasMessage) {
        artifact('\n\n')
      }
      artifact(value)
      hasMessage = true
    },
  }
}

export async function defaultSdkFactory (): Promise<SdkClient> {
  const executable = fileURLToPath(import.meta.resolve(`@github/copilot-${process.platform}-${process.arch}`))
  return new CopilotClient({ connection: RuntimeConnection.forStdio({ path: executable }) })
}

export function permissionFor (
  repo: string,
  sampleRoot: string,
  writeEnabled: boolean,
  customTools: string[],
  request: PermissionRequest
): PermissionRequestResult {
  if (request.kind === 'read') {
    try {
      const root = realpathSync(repo)
      const candidate = realpathSync(path.resolve(repo, request.path))
      const relative = path.relative(root, candidate)
      const segments = relative.split(path.sep)
      return {
        kind:
          candidate === root ||
          (!relative.startsWith('..') &&
            !path.isAbsolute(relative) &&
            !segments.includes('.git') &&
            !segments.includes('.codex'))
            ? 'approve-once'
            : 'reject',
      }
    } catch {
      return { kind: 'reject' }
    }
  }
  if (request.kind === 'url') {
    try {
      const url = new URL(request.url)
      const official = [
        'learn.microsoft.com',
        'developer.microsoft.com',
        'docs.github.com',
        'github.com',
        'raw.githubusercontent.com',
        'www.nuget.org',
        'api.nuget.org',
      ]
      return {
        kind:
          url.protocol === 'https:' && official.includes(url.hostname) && !url.username && !url.password
            ? 'approve-once'
            : 'reject',
      }
    } catch {
      return { kind: 'reject' }
    }
  }
  if (request.kind === 'custom-tool') {
    return { kind: customTools.includes(request.toolName) ? 'approve-once' : 'reject' }
  }
  if (!writeEnabled || request.kind !== 'write' || request.requestSandboxBypass) {
    return { kind: 'reject' }
  }
  try {
    const root = realpathSync(path.join(repo, sampleRoot))
    const candidate = path.resolve(repo, request.fileName)
    if (
      path
        .relative(root, candidate)
        .split(path.sep)
        .some((part) => ['.git', '.codex', '.agents'].includes(part))
    ) {
      return { kind: 'reject' }
    }
    let ancestor = candidate
    while (!existsSync(ancestor)) {
      const parent = path.dirname(ancestor)
      if (parent === ancestor) {
        return { kind: 'reject' }
      }
      ancestor = parent
    }
    const resolved = path.resolve(realpathSync(ancestor), path.relative(ancestor, candidate))
    return { kind: resolved.startsWith(root + path.sep) ? 'approve-once' : 'reject' }
  } catch {
    return { kind: 'reject' }
  }
}

class PersistentImplementationSession implements ImplementationSession {
  private writable = false
  constructor (
    private readonly session: SdkSession,
    log: ReturnType<typeof createCopilotLog>,
    observed: ObservedModel[],
    private readonly status: (value: string) => void
  ) {
    session.on('assistant.message', (event) => {
      const data = event as { data?: { content?: unknown } }
      if (typeof data.data?.content === 'string') {
        log.write(data.data.content)
      }
    })
    session.on('assistant.usage', (event) => {
      if (event.data.model) {
        observed.push({ model: event.data.model, reasoningEffort: event.data.reasoningEffort ?? 'unknown' })
      }
    })
  }

  setWriteAccess (enabled: boolean): void {
    this.writable = enabled
  }

  canWrite (): boolean {
    return this.writable
  }

  async send (message: string, phase?: string): Promise<string> {
    if (phase) {
      this.status(`[Copilot] ${phase}...\n`)
    }
    const response = await this.session.sendAndWait({ prompt: message }, 30 * 60_000)
    if (phase) {
      this.status(`[Copilot] ${phase} complete.\n`)
    }
    return typeof response?.data.content === 'string' ? response.data.content.trim() : ''
  }

  async abort (): Promise<void> {
    await this.session.abort()
  }

  async close (): Promise<void> {
    await this.session.disconnect()
  }
}

export class CopilotAgentRunner {
  private client: SdkClient | undefined
  constructor (
    private readonly repo: string,
    private readonly sampleRoot: string,
    private readonly configuration: CopilotConfiguration,
    private readonly logFile: string,
    private readonly observed: ObservedModel[],
    private readonly skillDirectories: string[],
    private readonly factory: SdkFactory = defaultSdkFactory,
    private readonly guard: () => void = () => {},
    private readonly status: (value: string) => void = (value) => process.stdout.write(value)
  ) {}

  async open (tools: Tool[] = []): Promise<ImplementationSession> {
    if (!this.client) {
      const client = await this.factory()
      try {
        await client.start()
        const status = await client.getStatus()
        if (status.version !== this.configuration.runtimeVersion) {
          throw new SyncError(
            `Copilot runtime version mismatch: expected ${this.configuration.runtimeVersion}, received ${status.version}`
          )
        }
        if (!(await client.getAuthStatus()).isAuthenticated) {
          throw new SyncError('Copilot runtime is not authenticated')
        }
        this.client = client
      } catch (error) {
        await client.stop().catch(() => {})
        throw error
      }
    }
    const log = createCopilotLog((value) => appendFileSync(this.logFile, value, 'utf8'))
    const prompt = readFileSync(path.join(this.repo, 'automation/teams-sample-sync/prompts/agent-prompt.md'), 'utf8')
    const names = tools.map((tool) => tool.name)
    const availableTools = new ToolSet().addBuiltIn([
      'view',
      'grep',
      'glob',
      'skill',
      'web_fetch',
      'edit',
      'apply_patch',
      'create',
      'str_replace_editor',
    ])
    for (const name of names) {
      availableTools.addCustom(name)
    }
    const raw = await this.client.createSession({
      // TODO: Set capi.autoTier to "balance" after https://github.com/github/copilot-sdk/issues/2559 is fixed.
      model: 'auto',
      workingDirectory: this.repo,
      skillDirectories: this.skillDirectories,
      systemMessage: { mode: 'append', content: prompt },
      tools,
      availableTools,
      excludedTools: ['shell', 'bash', 'terminal'],
      enableConfigDiscovery: false,
      hooks: {
        onPreToolUse: (input) => {
          this.guard()
          if (this.isWriteTool(input.toolName) && !active?.canWrite()) {
            return {
              permissionDecision: 'deny',
              permissionDecisionReason: 'The migration plan is being drafted; do not edit before it is frozen.',
            }
          }
          return undefined
        },
        onPostToolUse: () => {
          this.guard()
        },
      },
      onPermissionRequest: (request) =>
        permissionFor(this.repo, this.sampleRoot, active?.canWrite() ?? false, names, request),
    })
    const active = new PersistentImplementationSession(raw, log, this.observed, this.status)
    return active
  }

  private isWriteTool (name: string): boolean {
    return ['edit', 'apply_patch', 'create', 'str_replace_editor'].includes(name)
  }

  async close (): Promise<void> {
    if (this.client) {
      await this.client.stop()
      this.client = undefined
    }
  }
}

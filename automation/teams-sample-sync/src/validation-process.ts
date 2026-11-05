/** Bounded validation processes and shared cancellation generation. */
import { spawn, spawnSync } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { SyncError } from './config.js'
import type { Language } from './identity.js'

export function sanitizedChildEnvironment (extra: Record<string, string> = {}): NodeJS.ProcessEnv {
  const retained = [
    'PATH',
    'Path',
    'SystemRoot',
    'WINDIR',
    'HOME',
    'USERPROFILE',
    // NuGet needs user and machine configuration directories on Windows.
    'APPDATA',
    'LOCALAPPDATA',
    'ProgramFiles',
    'ProgramFiles(x86)',
    'ProgramW6432',
    'TMP',
    'TEMP',
    'DOTNET_ROOT',
    'DOTNET_CLI_HOME',
    'NUGET_PACKAGES',
    'SSL_CERT_FILE',
  ]
  const environment: NodeJS.ProcessEnv = {}

  for (const key of retained) {
    if (process.env[key] !== undefined) {
      environment[key] = process.env[key]
    }
  }

  for (const [key, value] of Object.entries(extra)) {
    environment[key] = value
  }

  return environment
}

const activeProcesses = new Set<ReturnType<typeof spawn>>()
let validationGeneration = 0

export function currentValidationGeneration (): number {
  return validationGeneration
}

export function assertValidationActive (generation: number): void {
  if (generation !== validationGeneration) {
    throw new SyncError('Validation cancelled')
  }
}

const networkFailure = new RegExp([
  'NU13(?:00|01)',
  'unable to load the service index',
  'name or service not known',
  'temporary failure in name resolution',
  'connection (?:refused|timed out)',
  'TLS handshake',
  'network is unreachable',
  'EAI_AGAIN|ENOTFOUND|ECONNRESET|ETIMEDOUT|CERT_HAS_EXPIRED|UNABLE_TO_VERIFY_LEAF_SIGNATURE',
].join('|'), 'i')

export function cancelValidationProcesses (): void {
  validationGeneration += 1

  for (const child of activeProcesses) {
    terminate(child)
  }
}

export async function commandErrors (
  command: string,
  args: string[],
  cwd: string,
  timeoutMs = 10 * 60_000
): Promise<string[]> {
  const generation = validationGeneration

  return new Promise((resolve, reject) => {
    let executable = command
    let arguments_ = args

    if (command === 'npm' && process.platform === 'win32') {
      const cli = process.env.npm_execpath?.endsWith('npm-cli.js')
        ? process.env.npm_execpath
        : path.join(path.dirname(process.execPath), 'node_modules/npm/bin/npm-cli.js')

      if (!existsSync(cli)) {
        reject(new SyncError('Cannot locate npm-cli.js for bounded Windows validation'))

        return
      }

      executable = process.execPath
      arguments_ = [cli, ...args]
    }

    const child = spawn(executable, arguments_, {
      cwd,
      detached: process.platform !== 'win32',
      env: sanitizedChildEnvironment(),
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    activeProcesses.add(child)
    let output = ''
    let timedOut = false
    const timer = setTimeout(() => {
      timedOut = true
      terminate(child)
    }, timeoutMs)
    const append = (chunk: Buffer): void => {
      output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
    }

    child.stdout.on('data', append)
    child.stderr.on('data', append)
    const cleanup = (): void => {
      clearTimeout(timer)
      activeProcesses.delete(child)
    }

    child.on('error', (error) => {
      cleanup()
      reject(new SyncError(`Cannot run ${command}: ${error.message}`))
    })
    child.on('close', (code) => {
      cleanup()

      if (generation !== validationGeneration) {
        reject(new SyncError(`Validation command cancelled: ${command}`))

        return
      }

      if (timedOut) {
        reject(new SyncError(`Validation command timed out: ${command} ${args.join(' ')}`))

        return
      }

      if (code === 0) {
        resolve([])

        return
      }

      if (networkFailure.test(output)) {
        reject(new SyncError(`Validation infrastructure failed while running ${command}:\n${output.trim()}`))

        return
      }

      resolve([`${command} ${args.join(' ')} failed:\n${output.trim()}`])
    })
  })
}

function terminate (child: ReturnType<typeof spawn>): void {
  if (!child.pid) {
    return
  }

  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], {
      stdio: 'ignore',
      windowsHide: true
    })

    return
  }

  try {
    process.kill(-child.pid, 'SIGKILL')

    return
  } catch {
    /* Child may not own a process group. */
  }

  child.kill('SIGKILL')
}

export async function httpSmoke (sampleRoot: string, project: string, language: Language = 'dotnet'): Promise<string[]> {
  const generation = validationGeneration
  const port = 41000 + Math.floor(Math.random() * 10000)
  const url = `http://127.0.0.1:${port}`
  const command = language === 'dotnet' ? 'dotnet' : process.execPath
  const args = language === 'dotnet' ? ['run', '--project', project, '--no-build', '--no-restore', '--urls', url] : [project]
  const child = spawn(command, args, {
    cwd: sampleRoot,
    env: sanitizedChildEnvironment({
      ASPNETCORE_URLS: url,
      ASPNETCORE_ENVIRONMENT: 'Development',
      PORT: String(port)
    }),
    stdio: ['ignore', 'pipe', 'pipe'],
    detached: process.platform !== 'win32',
  })
  activeProcesses.add(child)
  let spawnError: Error | undefined
  child.on('error', (error) => {
    spawnError = error
  })
  let output = ''
  child.stdout.on('data', (chunk: Buffer) => {
    output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
  })
  child.stderr.on('data', (chunk: Buffer) => {
    output = (output + chunk.toString('utf8')).slice(-1024 * 1024)
  })

  try {
    for (let count = 0; count < 40; count += 1) {
      assertValidationActive(generation)

      if (spawnError) {
        throw new SyncError(`Cannot start HTTP smoke process: ${spawnError.message}`)
      }

      if (child.exitCode !== null) {
        return [`HTTP smoke process exited before readiness:\n${output.trim()}`]
      }

      try {
        const response = await fetch(`${url}/`, { signal: AbortSignal.timeout(500) })

        if (response.status === 200) {
          return []
        }
      } catch {
        /* Wait for startup. */
      }

      await delay(500)
    }

    return [`HTTP smoke GET / did not return 200:\n${output.trim()}`]
  } finally {
    terminate(child)
    activeProcesses.delete(child)
  }
}

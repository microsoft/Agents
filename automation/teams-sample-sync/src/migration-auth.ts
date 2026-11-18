// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Retry only a rejected organization-billed planning request, before a plan exists. */
import {
  cpSync, mkdirSync, mkdtempSync, renameSync, rmSync
} from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'

export function isolateAuthenticationFailure (output: string): () => void {
  const temporary = mkdtempSync(path.join(tmpdir(), 'teams-sync-auth-'))
  const archived = path.join(temporary, 'automatic-token')

  try {
    renameSync(output, archived)
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== 'EXDEV') {
      rmSync(temporary, {
        recursive: true,
        force: true
      })
      throw error
    }

    cpSync(output, archived, { recursive: true })
    rmSync(output, {
      recursive: true,
      force: true
    })
  }

  return () => {
    mkdirSync(output, { recursive: true })
    cpSync(archived, path.join(output, 'authentication-attempt'), { recursive: true })
    rmSync(temporary, {
      recursive: true,
      force: true
    })
  }
}

export interface MigrationAttempt {
  code: number
  error?: string
  planningCompleted: boolean
}

export async function withMigrationCredentialFallback (
  primary: string | undefined,
  fallback: string | undefined,
  run: (token: string | undefined) => Promise<MigrationAttempt>,
  report: (message: string) => void = (message) => process.stdout.write(message),
  beforeRetry: () => (() => void) = () => () => {}
): Promise<number> {
  const result = await run(primary)
  const policyRejected = result.code !== 0 && !result.planningCompleted &&
    /get_model_list/.test(result.error ?? '') &&
    /"status"\s*:\s*403/.test(result.error ?? '') &&
    (result.error ?? '').includes('Allow use of Copilot CLI billed to the organization')

  if (!policyRejected || !fallback || fallback === primary) {
    return result.code
  }

  report('[Copilot] Organization billing policy rejected the automatic token; retrying planning once with the configured PAT.\n')

  const finish = beforeRetry()

  try {
    return (await run(fallback)).code
  } finally {
    finish()
  }
}

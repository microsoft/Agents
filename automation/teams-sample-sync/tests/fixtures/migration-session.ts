/** Session doubles and validation evidence shared by migration protocol tests. */
import type { ImplementationSession } from '../../src/agent-runner.js'
import type { ValidationResult } from '../../src/types.js'

export function validation (passed: boolean, digest: string): ValidationResult {
  return {
    version: 3,
    language: 'dotnet',
    id: digest,
    sample: 'sample-a',
    group: 'all',
    passed,
    repairable: true,
    outputDigest: digest,
    errors: passed ? [] : ['Build failed'],
    externalValidationRequired: [],
    checks: {
      project: {
        status: 'passed',
        errors: []
      },
      restore: {
        status: 'passed',
        errors: []
      },
      build: {
        status: passed ? 'passed' : 'failed',
        errors: passed ? [] : ['Build failed']
      },
      manifest: {
        status: 'passed',
        errors: []
      },
      httpSmoke: {
        status: 'passed',
        errors: []
      },
      contracts: {
        status: 'passed',
        errors: []
      },
      sampleTests: {
        status: 'skipped',
        errors: []
      },
    },
  }
}

export class FakeSession implements ImplementationSession {
  writable = false
  readonly prompts: string[] = []
  readonly phases: Array<string | undefined> = []
  readonly writableStates: boolean[] = []
  constructor (private readonly answers: Array<string | Error>) {}
  setWriteAccess (enabled: boolean): void {
    this.writable = enabled
  }

  async send (message: string, phase?: string): Promise<string> {
    this.prompts.push(message)
    this.phases.push(phase)
    this.writableStates.push(this.writable)
    const answer = this.answers.shift() ?? ''
    if (answer instanceof Error) {
      throw answer
    }

    return answer
  }

  async close (): Promise<void> {}
  async abort (): Promise<void> {}
}

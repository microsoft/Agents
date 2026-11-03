/** .NET restore/build, runtime smoke and protected behavior contracts. */
import { existsSync, readdirSync, rmSync } from 'node:fs'
import path from 'node:path'
import { checkProject, checkRestoredPackages } from './dotnet-project.js'
import { dotnetCapabilities } from './dotnet-capabilities.js'
import type { LanguageValidation, CodeValidationInput } from './language-validation.js'
import type { ValidationCheck } from './types.js'

const CONTRACT_SAMPLES = [
  'agent-targeted-messages',
  'bot-ai-messages',
  'bot-attachments',
  'bot-cards',
  'bot-meetings',
  'bot-message-extensions',
  'bot-task-modules',
]

function hasDotnetContracts (sample: string): boolean {
  return CONTRACT_SAMPLES.includes(sample)
}

async function validateCode (input: CodeValidationInput): Promise<Record<string, ValidationCheck>> {
  const {
    repo, sample, sampleRoot, configured, selectedAgentsSdkVersion, runtime, group
  } = input
  const runCommand = runtime.runCommand
  const checks: Record<string, ValidationCheck> = {}
  const recordCheck = (name: string, failures: string[]): void => {
    checks[name] = {
      status: failures.length ? 'failed' : 'passed',
      errors: failures
    }
  }

  const skipCheck = (name: string, reason?: string): void => {
    checks[name] = {
      status: reason ? 'not-run' : 'skipped',
      errors: reason ? [reason] : []
    }
  }

  let project: string | undefined
  let buildPassed = false

  if (group !== 'manifest') {
    const projectCheck = checkProject(sampleRoot, configured, selectedAgentsSdkVersion)
    project = projectCheck.project
    recordCheck('project', projectCheck.errors)

    if (project) {
      const projectArgument = path.relative(sampleRoot, project)
      rmSync(path.join(path.dirname(project), 'obj', 'project.assets.json'), { force: true })
      const restore = await runCommand('dotnet', ['restore', projectArgument, '--nologo'], sampleRoot)

      if (restore.length === 0) {
        restore.push(...checkRestoredPackages(project, selectedAgentsSdkVersion))
      }

      recordCheck('restore', restore)

      if (restore.length === 0) {
        const build = await runCommand(
          'dotnet',
          ['build', projectArgument, '--no-restore', '--nologo', '--warnaserror'],
          sampleRoot
        )
        recordCheck('build', build)
        buildPassed = build.length === 0
      } else {
        skipCheck('build', 'Build requires a successful restore')
      }
    } else {
      skipCheck('restore', 'Restore requires a project')
      skipCheck('build', 'Build requires a project')
    }
  } else {
    for (const name of ['project', 'restore', 'build']) {
      skipCheck(name)
    }
  }

  if (group === 'all') {
    if (buildPassed && project) {
      recordCheck('httpSmoke', await runtime.runHttpSmoke(sampleRoot, path.relative(sampleRoot, project)))
    } else {
      skipCheck('httpSmoke', 'HTTP smoke requires a successful build')
    }
  } else {
    skipCheck('httpSmoke')
  }

  const hasContracts = hasDotnetContracts(sample)

  if (group !== 'manifest' && hasContracts) {
    if (buildPassed) {
      recordCheck(
        'contracts',
        await runCommand(
          'dotnet',
          [
            'test',
            path.join(repo, configured.contractDirectory, 'TeamsSampleSync.ContractTests.csproj'),
            '--nologo',
            '--warnaserror',
            `-p:AgentsSdkTestVersion=${selectedAgentsSdkVersion}`,
            '--filter',
            '(Sample=' + sample + ')|(Category=TeamsHarness)',
          ],
          repo
        )
      )
    } else {
      skipCheck('contracts', 'Contract tests require a successful build')
    }
  } else {
    skipCheck('contracts')
  }

  const testsRoot = path.join(sampleRoot, 'tests')
  const testProjects = existsSync(testsRoot) ? readdirSync(testsRoot).filter((name) => name.endsWith('.csproj')) : []

  if (group !== 'manifest' && testProjects.length > 0) {
    if (testProjects.length !== 1) {
      recordCheck('sampleTests', ['Expected exactly one tests/*.csproj when sample tests are present'])
    } else if (!buildPassed) {
      skipCheck('sampleTests', 'Sample tests require a successful build')
    } else {
      recordCheck(
        'sampleTests',
        await runCommand(
          'dotnet',
          ['test', path.relative(sampleRoot, path.join(testsRoot, testProjects[0]!)), '--nologo', '--warnaserror'],
          sampleRoot
        )
      )
    }
  } else {
    skipCheck('sampleTests')
  }

  return checks
}

export const dotnetValidation: LanguageValidation = {
  validateCode,
  capabilities: dotnetCapabilities,
  requiredChecks (sample) {
    const checks = ['project', 'restore', 'build', 'manifest', 'httpSmoke']

    if (hasDotnetContracts(sample)) {
      checks.push('contracts')
    }

    return checks
  },
}

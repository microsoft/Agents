/** Extracts the C# routing conventions used by the migrated .NET samples. */
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { sourceFiles } from './source-files.js'
import type { SourceCapabilities } from './language-validation.js'

export function dotnetCapabilities (sampleRoot: string): SourceCapabilities {
  const sources = sourceFiles(sampleRoot)
    .filter((file) => file.endsWith('.cs') && path.relative(sampleRoot, file).split(path.sep)[0] !== 'tests')
    .map((file) => readFileSync(file, 'utf8'))
    .join('\n')

  return {
    bot: sources.includes('AgentApplication'),
    files: /TeamsFileConsent(?:Accept|Decline)Route/.test(sources),
    composeExtensions: /Teams(?:Query|SubmitAction|FetchAction|QueryLink|SelectItem)Route/.test(sources),
    queryCommands: [...sources.matchAll(/\[TeamsQueryRoute\s*\(\s*"([^"]+)"/g)].map((match) => ({
      id: match[1]!,
      evidence: `TeamsQueryRoute "${match[1]!}"`,
    })),
    ...(/\[TeamsQueryLinkRoute(?:\]|\s)/.test(sources) ? { linkHandlerEvidence: 'TeamsQueryLinkRoute' } : {}),
  }
}

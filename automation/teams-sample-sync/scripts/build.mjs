/** Build fresh automation output so removed source files cannot leave stale modules. */
import { spawnSync } from 'node:child_process'
import { rmSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const toolRoot = fileURLToPath(new URL('../', import.meta.url))
const outputDirectory = path.join(toolRoot, 'dist')
const compiler = path.join(toolRoot, 'node_modules/typescript/bin/tsc')

rmSync(outputDirectory, {
  recursive: true,
  force: true
})

const build = spawnSync(process.execPath, [compiler, '--project', 'tsconfig.build.json'], {
  cwd: toolRoot,
  stdio: 'inherit',
})
if (build.error) {
  throw build.error
}

process.exitCode = build.status ?? 1

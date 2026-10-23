// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { test } from 'node:test'

const packageJson = JSON.parse(readFileSync('package.json', 'utf8')) as {
  main: string
  scripts: Record<string, string>
}

test('package entry points exist after build', () => {
  const commands = [packageJson.main, packageJson.scripts.start, packageJson.scripts['start:local']]
  const entryPoints = commands.flatMap((command) => command.match(/\.\/dist\/[\w/-]+\.js/g) ?? [])

  assert.ok(entryPoints.length > 0)
  for (const entryPoint of entryPoints) {
    assert.ok(existsSync(entryPoint), `Missing generated entry point: ${entryPoint}`)
  }
})

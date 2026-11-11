import { strict as assert } from 'node:assert'
import { readFileSync, readdirSync } from 'node:fs'
import { describe, it } from 'node:test'
import Ajv2020 from 'ajv/dist/2020.js'

function readJson (path) {
  return JSON.parse(readFileSync(path, 'utf8'))
}

const schemaPath = process.env.AGENTS_CONFIGURATION_SCHEMA ??
  new URL('../agents-configuration.schema.json', import.meta.url)
const schema = readJson(schemaPath)
const ajv = new Ajv2020({ strict: true, allErrors: true })
assert.ok(ajv.validateSchema(schema), 'configuration schema must validate against its meta-schema')
const validate = ajv.compile(schema)

function safeErrors () {
  return (validate.errors ?? []).map(({ instancePath, keyword }) => ({
    instancePath,
    keyword
  }))
}

describe('configuration schema', () => {
  it('keeps registry-specific resolved URLs out of the lockfile', () => {
    const lockfile = readJson(new URL('../package-lock.json', import.meta.url))
    for (const entry of Object.values(lockfile.packages)) {
      assert.ok(!Object.hasOwn(entry, 'resolved'), 'lockfile must not pin a package registry URL')
    }
  })

  it('uses Draft 2020-12 and only local schema references', () => {
    assert.equal(schema.$schema, 'https://json-schema.org/draft/2020-12/schema')
    const nodes = [schema]
    while (nodes.length > 0) {
      const node = nodes.pop()
      if (node === null || typeof node !== 'object') continue
      if ('$ref' in node) {
        assert.ok(node.$ref.startsWith('#/'), 'schema references must resolve within the artifact')
      }
      nodes.push(...Object.values(node))
    }
  })

  it('annotates secrets without declaring runtime defaults', () => {
    assert.equal(schema.$defs.connectionSettings.properties.clientSecret.writeOnly, true)
    const nodes = [schema]
    while (nodes.length > 0) {
      const node = nodes.pop()
      if (node === null || typeof node !== 'object') continue
      assert.ok(!Object.hasOwn(node, 'default'), 'runtime defaults are outside the structural contract')
      nodes.push(...Object.values(node))
    }
  })

  it('reports validation paths and keywords without secret values', () => {
    const secret = 'synthetic-value-that-must-not-appear-in-diagnostics'
    assert.ok(!validate({
      connections: {
        example: {
          settings: { clientSecret: secret, scopes: [secret, 42] }
        }
      }
    }))
    const diagnostics = JSON.stringify(safeErrors())
    assert.ok(!diagnostics.includes(secret), 'diagnostics must not include configuration values')
    assert.ok(diagnostics.includes('/connections/example/settings/scopes/1'))
  })

  for (const name of readdirSync(new URL('../examples/', import.meta.url)).sort()) {
    if (!name.endsWith('.json')) continue
    it(`accepts example ${name}`, () => {
      const document = readJson(new URL(`../examples/${name}`, import.meta.url))
      assert.ok(validate(document), JSON.stringify(safeErrors()))
    })
  }

  for (const { name, document } of readJson(new URL('valid/cases.json', import.meta.url))) {
    it(`accepts ${name}`, () => {
      const original = JSON.stringify(document)
      assert.ok(validate(document), JSON.stringify(safeErrors()))
      assert.ok(JSON.stringify(document) === original, 'validation must not mutate configuration')
    })
  }

  for (const { name, document, expectedError } of readJson(new URL('invalid/cases.json', import.meta.url))) {
    it(`rejects ${name}`, () => {
      assert.ok(!validate(document), 'invalid fixture must fail validation')
      assert.ok(
        safeErrors().some(({ instancePath, keyword }) =>
          instancePath === expectedError.instancePath && keyword === expectedError.keyword),
        `expected ${JSON.stringify(expectedError)}; got ${JSON.stringify(safeErrors())}`
      )
    })
  }
})

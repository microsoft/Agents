import fs from 'fs'
import path from 'path'
import Ajv from 'ajv'
import addFormats from 'ajv-formats'

const __dirname = path.dirname(new URL(import.meta.url).pathname.substring(1))

// Initialize AJV
const ajv = new Ajv({ allErrors: true, discriminator: true })
addFormats(ajv)

// Load the schema
const schemaPath = path.join(__dirname, '../../activity-protocol.schema.json')
const schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'))

// Compile the schema
const validate = ajv.compile(schema)

// Collect examples from markdown
const examples = []
const mdExamplesPath = path.join(__dirname, '../../activity-examples.md')
if (fs.existsSync(mdExamplesPath)) {
  const examplesContent = fs.readFileSync(mdExamplesPath, 'utf8')
  const codeBlockRegex = /```json\n([\s\S]*?)\n```/g
  let match
  while ((match = codeBlockRegex.exec(examplesContent)) !== null) {
    try {
      examples.push({ source: 'markdown', data: JSON.parse(match[1]) })
    } catch (e) {
      console.error('Failed to parse JSON example from markdown:', e)
    }
  }
}

// Collect standalone JSON example files
const jsonExamplesDir = path.join(__dirname, '../examples/json')
if (fs.existsSync(jsonExamplesDir)) {
  for (const file of fs.readdirSync(jsonExamplesDir)) {
    if (file.toLowerCase().endsWith('.json')) {
      try {
        const data = JSON.parse(fs.readFileSync(path.join(jsonExamplesDir, file), 'utf8'))
        examples.push({ source: file, data })
      } catch (e) {
        console.error(`Failed to parse JSON example file ${file}:`, e)
      }
    }
  }
}

if (examples.length === 0) {
  console.warn('No examples found to validate.')
  process.exit(0)
}

// Validate each example
let allValid = true
examples.forEach((example, index) => {
  const valid = validate(example.data)
  const label = example.data.type || 'unknown'
  const origin = example.source
  if (valid) {
    console.log(`Example ${index + 1} [${origin}] (${label}) is valid.`)
  } else {
    allValid = false
    console.error(`Example ${index + 1} [${origin}] (${label}) is invalid:`)
    console.error(validate.errors)
  }
  console.log('---')
})

if (allValid) {
  console.log('All examples are valid!')
} else {
  console.error('Some examples are invalid.')
  process.exit(1)
}

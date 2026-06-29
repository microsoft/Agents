/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

// Ensures the settings.js file exists before building the project to avoid
// module not found errors. Mostly useful for CI builds.
const fs = require('fs')
const path = require('path')

const src = path.resolve('./src/settings.EXAMPLE.js')
const settings = path.resolve('./src/settings.js')

if (fs.existsSync(settings)) {
  process.exit(0)
}

if (!fs.existsSync(src)) {
  console.error('settings.EXAMPLE.js not found.')
  process.exit(1)
}

fs.copyFileSync(src, settings)
console.log('Created settings.js from settings.EXAMPLE.js. Please update your configuration.')

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/** Python source capability analysis uses the trusted non-executing AST inspector. */
import { inspectPythonProject } from './python-project.js'
import type { PythonToolRuntime } from './python-tool.js'
import type { SourceCapabilities } from './language-validation.js'

export function pythonCapabilities (runtime: PythonToolRuntime, sampleRoot: string): SourceCapabilities {
  return inspectPythonProject(runtime, sampleRoot, {}).capabilities
}

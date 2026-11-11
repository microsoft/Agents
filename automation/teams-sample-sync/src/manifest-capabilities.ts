/** Compares normalized source capabilities with the manifest declarations they require. */
import type { SourceCapabilities } from './language-validation.js'
import type { ManifestTarget } from './types.js'

function manifestComposeCommands (manifest: Record<string, unknown>): Map<string, string[]> {
  const commands = new Map<string, string[]>()

  if (!Array.isArray(manifest.composeExtensions)) {
    return commands
  }

  for (const extension of manifest.composeExtensions) {
    if (!extension || typeof extension !== 'object') {
      continue
    }

    const entries = (extension as Record<string, unknown>).commands

    if (!Array.isArray(entries)) {
      continue
    }

    for (const entry of entries) {
      if (!entry || typeof entry !== 'object') {
        continue
      }

      const command = entry as Record<string, unknown>

      if (typeof command.id === 'string' && typeof command.type === 'string') {
        commands.set(command.id, [...(commands.get(command.id) ?? []), command.type])
      }
    }
  }

  return commands
}

function manifestLinkDomains (manifest: Record<string, unknown>): string[] {
  if (!Array.isArray(manifest.composeExtensions)) {
    return []
  }

  return manifest.composeExtensions.flatMap((extension) => {
    if (!extension || typeof extension !== 'object') {
      return []
    }

    const handlers = (extension as Record<string, unknown>).messageHandlers

    if (!Array.isArray(handlers)) {
      return []
    }

    return handlers.flatMap((handler) => {
      if (!handler || typeof handler !== 'object' || (handler as Record<string, unknown>).type !== 'link') {
        return []
      }

      const value = (handler as Record<string, unknown>).value
      const domains = value && typeof value === 'object' ? (value as Record<string, unknown>).domains : undefined

      return Array.isArray(domains) ? domains.filter((domain): domain is string => typeof domain === 'string') : []
    })
  })
}

export function capabilityErrors (
  manifest: Record<string, unknown>,
  capabilities: SourceCapabilities,
  manifestTarget: ManifestTarget
): string[] {
  const errors = [...(capabilities.errors ?? [])]

  if (capabilities.resourceSpecificPermissions?.length) {
    const webApp = manifest.webApplicationInfo as {
      id?: string
      resource?: string
    } | undefined

    if (!webApp?.id || !webApp.resource) {
      errors.push('Meeting event routes require webApplicationInfo with the bot ID and resource')
    }

    const authorization = manifest.authorization as {
      permissions?: {
        resourceSpecific?: Array<{
          name?: string
          type?: string
        }>
      }
    } | undefined
    const declared = authorization?.permissions?.resourceSpecific
    const grants = Array.isArray(declared) ? declared : []

    for (const permission of capabilities.resourceSpecificPermissions) {
      if (!grants.some((grant) => grant && typeof grant === 'object' && grant.name === permission && grant.type === 'Application')) {
        errors.push(`Meeting event routes require Application resource-specific permission ${permission}`)
      }
    }
  }

  if (capabilities.bot && (!Array.isArray(manifest.bots) || manifest.bots.length === 0)) {
    errors.push('Manifest bots capability does not match the Agents application source')
  }

  if (capabilities.files && (!Array.isArray(manifest.bots) || !manifest.bots.some((bot: unknown) =>
    !!bot && typeof bot === 'object' && (bot as Record<string, unknown>).supportsFiles === true
  ))) {
    errors.push('File consent routes require a bot declaration with supportsFiles enabled')
  }

  if (
    capabilities.composeExtensions &&
    (!Array.isArray(manifest.composeExtensions) || manifest.composeExtensions.length === 0)
  ) {
    errors.push('Manifest composeExtensions capability does not match message-extension routes')
  }

  const composeCommands = manifestComposeCommands(manifest)

  for (const { id: routeId, evidence } of capabilities.queryCommands) {
    const declarations = composeCommands.get(routeId) ?? []

    if (declarations.length !== 1 || declarations[0] !== 'query') {
      errors.push(`${evidence} requires exactly one composeExtensions command with type "query"`)
    }
  }

  for (const { id, evidence } of capabilities.actionCommands ?? []) {
    const declarations = composeCommands.get(id) ?? []

    if (declarations.length !== 1 || declarations[0] !== 'action') {
      errors.push(`${evidence} requires exactly one composeExtensions command with type "action"`)
    }
  }

  if (capabilities.linkHandlerEvidence) {
    const declaredDomains = manifestLinkDomains(manifest).sort()

    if (declaredDomains.length === 0) {
      errors.push(`${capabilities.linkHandlerEvidence} requires a composeExtensions link message handler with declared domains`)
    } else if (declaredDomains.some((domain) => domain.includes('*'))) {
      errors.push(`${capabilities.linkHandlerEvidence} link-handler domains must be exact domains, not wildcards`)
    } else if (!manifestTarget.expectedLinkDomains?.length) {
      errors.push(`${capabilities.linkHandlerEvidence} requires trusted expectedLinkDomains in target configuration`)
    } else if (JSON.stringify(declaredDomains) !== JSON.stringify([...manifestTarget.expectedLinkDomains].sort())) {
      errors.push(`${capabilities.linkHandlerEvidence} link-handler domains must match trusted expected domains: ${manifestTarget.expectedLinkDomains.join(', ')}`)
    }
  }

  return errors
}

/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import { ConnectionSettings, loadCopilotStudioConnectionSettingsFromEnv } from '@microsoft/agents-copilotstudio-client'

export class SampleConnectionSettings extends ConnectionSettings {
  public readonly appClientId: string = ''
  public readonly tenantId: string = ''
  public readonly authority: string = 'https://login.microsoftonline.com'
  public readonly useS2SConnection: boolean = false
  public readonly appClientSecret: string = ''

  constructor () {
    const settings = loadCopilotStudioConnectionSettingsFromEnv()
    super(settings)

    if (!process.env.appClientId) {
      throw new Error('appClientId is required')
    }

    if (!process.env.tenantId) {
      throw new Error('tenantId is required')
    }

    this.appClientId = process.env.appClientId!
    this.tenantId = process.env.tenantId!
    this.authority = process.env.authority ?? 'https://login.microsoftonline.com'
    this.useS2SConnection = process.env.useS2SConnection === 'true'
    this.appClientSecret = process.env.appClientSecret ?? ''

    if (this.useS2SConnection && !this.appClientSecret) {
      throw new Error('appClientSecret is required for S2S connection')
    }
  }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { DefaultAzureCredential } from '@azure/identity'
import { BlobsStorage } from '@microsoft/agents-hosting-storage-blob'
import type { AppConfig } from './config.js'

export function createStorage (config: AppConfig): BlobsStorage {
  if (config.blobContainerUrl) {
    return new BlobsStorage(
      config.blobContainerId,
      undefined,
      undefined,
      config.blobContainerUrl,
      new DefaultAzureCredential({ managedIdentityClientId: config.agentClientId })
    )
  }
  return new BlobsStorage(config.blobContainerId, config.blobConnectionString)
}

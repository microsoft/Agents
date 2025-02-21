/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */
import fs from 'fs'
import { ICachePlugin, TokenCacheContext } from '@azure/msal-node'

export class MsalCachePlugin implements ICachePlugin {
  private cacheLocation: string = ''
  constructor (cacheLocation: string) {
    this.cacheLocation = cacheLocation
  }

  async beforeCacheAccess (tokenCacheContext: TokenCacheContext): Promise<void> {
    return new Promise((resolve, reject) => {
      if (fs.existsSync(this.cacheLocation)) {
        fs.readFile(this.cacheLocation, 'utf-8', (error, data) => {
          if (error) {
            reject(error)
          } else {
            tokenCacheContext.tokenCache.deserialize(data)
            resolve()
          }
        })
      } else {
        fs.writeFile(this.cacheLocation, tokenCacheContext.tokenCache.serialize(), (error) => {
          if (error) {
            reject(error)
          }
        })
      }
    })
  }

  async afterCacheAccess (tokenCacheContext: TokenCacheContext): Promise<void> {
    return new Promise((resolve, reject) => {
      if (tokenCacheContext.cacheHasChanged) {
        fs.writeFile(this.cacheLocation, tokenCacheContext.tokenCache.serialize(), (error) => {
          if (error) {
            reject(error)
          }
          resolve()
        })
      } else {
        resolve()
      }
    })
  }
}

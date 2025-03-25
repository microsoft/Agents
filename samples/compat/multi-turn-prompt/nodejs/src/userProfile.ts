// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import { Attachment } from '@microsoft/agents-hosting'

export class UserProfile {
  constructor (
    public transport: string = '',
    public name: string = '',
    public age: number = 0,
    public picture?: Attachment
  ) {
    this.transport = transport
    this.name = name
    this.age = age
    this.picture = picture
  }
}

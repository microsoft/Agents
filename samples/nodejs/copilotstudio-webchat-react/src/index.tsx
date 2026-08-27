/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import './instrumentation'
import React from 'react'
import { createRoot } from 'react-dom/client'

const rootElement = document.getElementById('root')

if (!rootElement) {
  throw new Error('Root element not found')
}

const root = createRoot(rootElement)

import('./Chat').then(({ default: Chat }) => {
  root.render(
    <div style={{
      width: '100vw',
      height: '100vh',
      margin: 0,
    }}
    >
      <Chat />
    </div>
  )
})

/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License.
 */

import './instrumentation'
import React from 'react'
import ReactDOM from 'react-dom'

import('./Chat').then(({ default: Chat }) => {
  ReactDOM.render(
    <div style={{
      width: '100vw',
      height: '100vh',
      margin: 0,
    }}
    >
      <Chat />
    </div>, document.getElementById('root')
  )
})

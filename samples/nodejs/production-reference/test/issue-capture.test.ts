// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import test from 'node:test'
import { advanceIssueCapture, initialIssueCaptureState } from '../src/issue-capture.js'

test('support issue capture records summary and impact', () => {
  const summary = advanceIssueCapture(initialIssueCaptureState(), 'Service is down')
  assert.equal(summary.state.stage, 'impact')
  assert.equal(summary.state.summary, 'Service is down')

  const impact = advanceIssueCapture(summary.state, 'high')
  assert.equal(impact.state.stage, 'complete')
  assert.equal(impact.state.impact, 'high')
})

test('support issue capture rejects an unknown impact', () => {
  const summary = advanceIssueCapture(initialIssueCaptureState(), 'Service is slow')
  const result = advanceIssueCapture(summary.state, 'urgent')

  assert.equal(result.state.stage, 'impact')
  assert.equal(result.reply, 'Reply with low, medium, or high.')
})

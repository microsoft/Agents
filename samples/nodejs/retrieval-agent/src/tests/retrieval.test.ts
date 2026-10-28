// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import assert from 'node:assert/strict'
import test from 'node:test'
import { buildGenieResponses, handleBuildGenieMessage } from '../services/messageRoute.js'
import { createFilterExpression, getRetrievalOptions, retrieveSharePoint, type RetrievalResult } from '../services/retrievalClient.js'

const options = { sharePointSiteUrl: 'https://contoso.sharepoint.com/sites/Build', maximumNumberOfResults: 3 }

test('creates a configured site filter', () => {
  assert.equal(createFilterExpression(options.sharePointSiteUrl), 'path:"https://contoso.sharepoint.com/sites/Build/"')
  assert.throws(() => getRetrievalOptions({ RETRIEVAL_SHAREPOINT_SITE_URL: 'http://contoso.sharepoint.com/sites/Build' }))
})

test('retrieves mapped items with a delegated token', async () => {
  let request: RequestInit | undefined
  const result = await retrieveSharePoint('Pricing Analytics', async () => 'delegated-token', options, async (_input, init) => {
    request = init
    return new Response(JSON.stringify({ retrievalHits: [{ webUrl: 'https://contoso.sharepoint.com/session.docx', extracts: [{ text: 'Session details' }], resourceMetadata: { title: 'Pricing Analytics' } }] }))
  })

  assert.equal(result.status, 'success')
  assert.equal(result.items[0].title, 'Pricing Analytics')
  assert.equal((request?.headers as Record<string, string>).Authorization, 'Bearer delegated-token')
  assert.match(String(request?.body), /"dataSource":"sharePoint"/)
})

test('maps token and service failures to safe statuses', async () => {
  assert.equal((await retrieveSharePoint('Build', async () => { throw new Error('secret') }, options)).status, 'notSignedIn')
  assert.equal((await retrieveSharePoint('Build', async () => 'token', options, async () => new Response('', { status: 502 }))).status, 'serviceUnavailable')
  assert.equal((await retrieveSharePoint('Build', async () => 'token', options, async () => new Response(JSON.stringify({ retrievalHits: [] })))).status, 'noResults')
})

test('message route sends grounded text and a source card', async () => {
  const result: RetrievalResult = { status: 'success', items: [{ title: 'Pricing Analytics', extract: 'Session details', webUrl: 'https://contoso.sharepoint.com/session.docx' }] }
  const messages: string[] = []
  const cards: object[] = []
  await handleBuildGenieMessage(result, async text => { messages.push(text) }, async card => { cards.push(card) })
  assert.match(messages[0], /https:\/\/contoso\.sharepoint\.com\/session\.docx/)
  assert.equal(cards.length, 1)
  assert.match(JSON.stringify(cards[0]), /https:\/\/contoso\.sharepoint\.com\/session\.docx/)
})

for (const status of ['notSignedIn', 'noResults', 'serviceUnavailable'] as const) {
  test(`message route sends one safe message for ${status}`, async () => {
    const messages: string[] = []
    const cards: object[] = []
    await handleBuildGenieMessage({ status, items: [] }, async text => { messages.push(text) }, async card => { cards.push(card) })
    assert.deepEqual(messages, [buildGenieResponses[status]])
    assert.equal(cards.length, 0)
  })
}

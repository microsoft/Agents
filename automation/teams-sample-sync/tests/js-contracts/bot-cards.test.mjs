import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sampleHarness } from './harness.mjs'

describe('bot-cards', () => {
  it('card actions preserve nested forms and command precedence', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({ text: 'CARD ACTIONS and toggle visibility' })
    const attachment = result.sent[0].attachments[0]
    assert.equal(attachment.contentType, 'application/vnd.microsoft.card.adaptive')
    const card = attachment.content
    assert.equal(card.body[0].text, 'Adaptive Card Actions')
    assert.deepEqual(card.actions.map((action) => action.type), ['Action.OpenUrl', 'Action.ShowCard', 'Action.ShowCard'])
    assert.equal(card.actions[1].card.body[0].id, 'name')
    assert.equal(card.actions[1].card.actions[0].verb, 'submit_name')
    assert.match(card.actions[2].card.actions[0].card.body[0].text, /Welcome To Your New Card/)
  })

  it('visibility command targets the initially hidden text', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({ text: 'toggle visibility' })
    const card = result.sent[0].attachments[0].content
    assert.equal(card.body[1].id, 'helloWorld')
    assert.equal(card.body[1].isVisible, false)
    assert.deepEqual(card.actions[0].targetElements, ['helloWorld'])
  })

  it('form submission sends the name and the expected invoke response', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({
      type: 'invoke',
      name: 'adaptiveCard/action',
      value: {
        action: {
          type: 'Action.Execute',
          verb: 'submit_name',
          data: { name: 'Ada' }
        }
      },
    })
    assert.ok(result.sent.some((activity) => activity.text === 'Data Submitted: Ada'))
    assert.equal(result.invoke.value.status, 200)
    assert.deepEqual(result.invoke.value.body, {
      statusCode: 200,
      type: 'application/vnd.microsoft.activity.message',
      value: 'Action processed successfully',
    })
  })

  it('unknown text returns help containing both commands', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({ text: 'unknown command' })
    assert.match(result.sent[0].text, /card actions.*toggle visibility/)
  })
})

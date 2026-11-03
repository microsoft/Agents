import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sampleHarness, sampleHttp } from './harness.mjs'

describe('bot-task-modules', () => {
  it('messages expose all three task launch buttons', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({ text: 'show tasks' })
    const actions = result.sent[0].attachments[0].content.actions
    assert.deepEqual(actions.map((action) => action.data.data), ['AdaptiveCard', 'CustomForm', 'MultiStep'])
    assert.ok(actions.every((action) => action.data.msteams.type === 'task/fetch'))
  })

  it('task fetch selects text input, custom URL and multi-step name card', async () => {
    const harness = await sampleHarness({ endpoint: 'https://agent.example.com' })
    const fetchTask = async (type) => (await harness.turn({
      type: 'invoke',
      name: 'task/fetch',
      value: { data: { data: type } },
    })).invoke.value.body.task
    const input = await fetchTask('AdaptiveCard')
    assert.equal(input.value.card.content.body[1].id, 'usertext')
    const url = await fetchTask('CustomForm')
    assert.equal(url.value.url, 'https://agent.example.com/CustomForm/')
    assert.equal(url.value.fallbackUrl, url.value.url)
    const steps = await fetchTask('MultiStep')
    assert.equal(steps.value.card.content.body[1].id, 'name')
  })

  it('multi-step submission carries the name and returns the completion message', async () => {
    const harness = await sampleHarness()
    const first = await harness.turn({
      type: 'invoke',
      name: 'task/submit',
      value: {
        data: {
          submissiontype: 'multi_step_1',
          name: 'Ada'
        }
      }
    })
    const secondCard = first.invoke.value.body.task.value.card.content
    assert.equal(secondCard.body[1].id, 'email')
    assert.equal(secondCard.actions[0].data.name, 'Ada')
    const second = await harness.turn({
      type: 'invoke',
      name: 'task/submit',
      value: {
        data: {
          submissiontype: 'multi_step_2',
          name: 'Ada',
          email: 'ada@example.com',
        }
      }
    })
    assert.ok(second.sent.some((activity) => activity.text === 'Hi Ada, thanks for submitting! Your email is ada@example.com'))
    assert.equal(second.invoke.value.body.task.value, 'Multi-step form completed!')
  })

  it('custom-form and text-input submissions retain their response envelopes', async () => {
    const harness = await sampleHarness()
    const form = await harness.turn({
      type: 'invoke',
      name: 'task/submit',
      value: {
        data: {
          submissiontype: 'custom_form',
          name: 'Grace',
          email: 'grace@example.com',
        }
      }
    })
    assert.equal(form.invoke.value.body.task.value, 'Form submitted successfully')
    const text = await harness.turn({
      type: 'invoke',
      name: 'task/submit',
      value: { data: { usertext: 'Hello' } }
    })
    assert.ok(text.sent.some((activity) => activity.text === 'You submitted: Hello'))
    assert.equal(text.invoke.value.body.task.value, 'Thanks for submitting!')
  })

  it('the custom-form URL serves the actual Teams dialog page', async () => {
    await sampleHttp(async (origin) => {
      const response = await fetch(`${origin}/CustomForm/`)
      assert.equal(response.status, 200)
      const html = await response.text()
      assert.match(html, /id="name"/)
      assert.match(html, /id="email"/)
      assert.match(html, /microsoftTeams\.dialog\.url\.submit/)
      assert.match(html, /submissiontype: 'custom_form'/)
    })
  })
})

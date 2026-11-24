import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { sampleHarness } from './harness.mjs'

describe('bot-message-extensions', () => {
  it('search returns Adaptive Cards with sanitized snippets and thumbnail previews', async () => {
    let searched = ''
    const harness = await sampleHarness({
      searchWikipedia: async (query) => {
        searched = query

        return [{
          title: 'Computer science',
          snippet: '<b>Computing</b> history'
        }]
      }
    })
    const result = await harness.turn({
      type: 'invoke',
      name: 'composeExtension/query',
      value: {
        commandId: 'wikipediaSearch',
        parameters: [{
          name: 'query',
          value: 'computers'
        }]
      },
    })
    assert.equal(searched, 'computers')
    assert.equal(result.invoke.value.status, 200)
    const extension = result.invoke.value.body.composeExtension
    assert.equal(extension.type, 'result')
    assert.equal(extension.attachmentLayout, 'list')
    const attachment = extension.attachments[0]
    assert.equal(attachment.content.body[1].text, 'Computing history')
    assert.equal(attachment.preview.content.title, 'Computer science')
    assert.equal(attachment.content.body[2].actions[0].url, 'https://en.wikipedia.org/wiki/Computer_science')
  })

  it('empty search and unknown command IDs return the no-results response', async () => {
    const harness = await sampleHarness({ searchWikipedia: async () => [] })
    for (const commandId of ['wikipediaSearch', 'unknown-command']) {
      const result = await harness.turn({
        type: 'invoke',
        name: 'composeExtension/query',
        value: {
          commandId,
          parameters: [{
            name: 'query',
            value: 'no matches'
          }]
        },
      })
      assert.deepEqual(result.invoke.value.body, {
        composeExtension: {
          type: 'message',
          text: "No results found for 'no matches'"
        },
      })
    }
  })

  it('search snippets remove incomplete and nested markup from cards and previews', async () => {
    const cases = [
      ['<b>Computing</b> history', 'Computing history'],
      ['<script', 'script'],
      ['<<script>alert(1)<</script>', 'alert(1)'],
      ['one <> two > three <', 'one  two  three '],
    ]
    const harness = await sampleHarness({
      searchWikipedia: async () => cases.map(([snippet]) => ({
        title: 'Article',
        snippet
      })),
    })
    const result = await harness.turn({
      type: 'invoke',
      name: 'composeExtension/query',
      value: {
        commandId: 'wikipediaSearch',
        parameters: [{
          name: 'query',
          value: 'test'
        }]
      },
    })
    const attachments = result.invoke.value.body.composeExtension.attachments
    assert.equal(attachments.length, cases.length)
    for (const [index, [, expected]] of cases.entries()) {
      assert.equal(attachments[index].content.body[1].text, expected)
      assert.equal(attachments[index].preview.content.text, expected)
    }
  })

  it('link unfurl returns the requested URL and help retains the compose instructions', async () => {
    const harness = await sampleHarness()
    const result = await harness.turn({
      type: 'invoke',
      name: 'composeExtension/queryLink',
      value: { url: 'https://en.wikipedia.org/wiki/Computer_science' },
    })
    const attachment = result.invoke.value.body.composeExtension.attachments[0]
    assert.equal(attachment.content.body[0].text, 'Link Preview')
    assert.match(attachment.content.body[1].text, /Computer_science/)
    const help = await harness.turn({ text: 'HELP' })
    assert.match(help.sent[0].text, /compose area.*Wikipedia/)
    const echo = await harness.turn({ text: 'Hello' })
    assert.match(echo.sent[0].text, /You said: Hello/)
  })
})
